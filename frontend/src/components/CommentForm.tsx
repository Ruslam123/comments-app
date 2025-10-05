import React, { useState, useEffect } from 'react';
import './CommentForm.css';

interface CommentFormProps {
  onCommentAdded: () => void;
  parentId?: string;
}

const API_URL = process.env.REACT_APP_API_URL || 'http://localhost:5000';

const CommentForm: React.FC<CommentFormProps> = ({ onCommentAdded, parentId }) => {
  const [userName, setUserName] = useState('');
  const [email, setEmail] = useState('');
  const [homePage, setHomePage] = useState('');
  const [text, setText] = useState('');
  const [captchaToken, setCaptchaToken] = useState('');
  const [captchaCode, setCaptchaCode] = useState('');
  const [captchaImage, setCaptchaImage] = useState('');
  const [preview, setPreview] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [textFile, setTextFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);

  useEffect(() => {
    loadCaptcha();
  }, []);

  const loadCaptcha = async () => {
    const response = await fetch(`${API_URL}/api/captcha`);
    const data = await response.json();
    setCaptchaToken(data.token);
    setCaptchaImage(data.code);
  };

  const validateForm = (): boolean => {
    const newErrors: Record<string, string> = {};

    if (!/^[a-zA-Z0-9]+$/.test(userName)) {
      newErrors.userName = 'Только латинские буквы и цифры';
    }

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      newErrors.email = 'Неверный формат email';
    }

    if (homePage && !/^https?:\/\/.+/.test(homePage)) {
      newErrors.homePage = 'Неверный формат URL';
    }

    if (!text.trim()) {
      newErrors.text = 'Текст обязателен';
    }

    if (!captchaCode) {
      newErrors.captcha = 'Введите CAPTCHA';
    }

    // Валідація файлів
    if (imageFile) {
      const allowedImageTypes = ['image/jpeg', 'image/jpg', 'image/png', 'image/gif'];
      if (!allowedImageTypes.includes(imageFile.type)) {
        newErrors.imageFile = 'Дозволені тільки JPG, PNG, GIF';
      }
      if (imageFile.size > 5 * 1024 * 1024) {
        newErrors.imageFile = 'Розмір зображення не повинен перевищувати 5MB';
      }
    }

    if (textFile) {
      if (textFile.type !== 'text/plain' && !textFile.name.endsWith('.txt')) {
        newErrors.textFile = 'Дозволені тільки .txt файли';
      }
      if (textFile.size > 100 * 1024) {
        newErrors.textFile = 'Розмір файлу не повинен перевищувати 100KB';
      }
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handlePreview = async () => {
    const response = await fetch(`${API_URL}/api/comments/preview`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ text })
    });
    const data = await response.json();
    setPreview(data.html);
  };

  const uploadFiles = async (): Promise<{ imageUrl?: string; textFileUrl?: string }> => {
    const result: { imageUrl?: string; textFileUrl?: string } = {};

    if (imageFile) {
      const formData = new FormData();
      formData.append('file', imageFile);
      
      const response = await fetch(`${API_URL}/api/file/image`, {
        method: 'POST',
        body: formData
      });
      
      if (response.ok) {
        const data = await response.json();
        result.imageUrl = data.url;
      }
    }

    if (textFile) {
      const formData = new FormData();
      formData.append('file', textFile);
      
      const response = await fetch(`${API_URL}/api/file/text`, {
        method: 'POST',
        body: formData
      });
      
      if (response.ok) {
        const data = await response.json();
        result.textFileUrl = data.url;
      }
    }

    return result;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!validateForm()) return;

    setUploading(true);

    try {
      // Валідація CAPTCHA
      const captchaValid = await fetch(`${API_URL}/api/captcha/validate`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token: captchaToken, code: captchaCode })
      });
      const captchaResult = await captchaValid.json();

      if (!captchaResult.valid) {
        setErrors({ captcha: 'Неверный код CAPTCHA' });
        loadCaptcha();
        setUploading(false);
        return;
      }

      // Завантаження файлів
      const files = await uploadFiles();

      // Відправка коментаря
      const response = await fetch(`${API_URL}/api/comments`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          userName,
          email,
          homePage: homePage || null,
          text,
          captchaToken,
          parentCommentId: parentId || null,
          ...files
        })
      });

      if (response.ok) {
        // Очистити форму
        setUserName('');
        setEmail('');
        setHomePage('');
        setText('');
        setCaptchaCode('');
        setPreview('');
        setImageFile(null);
        setTextFile(null);
        loadCaptcha();
        onCommentAdded();
      } else {
        const error = await response.json();
        setErrors({ submit: error.message || 'Помилка відправки' });
      }
    } catch (error) {
      console.error('Error:', error);
      setErrors({ submit: 'Помилка з\'єднання з сервером' });
    } finally {
      setUploading(false);
    }
  };

  const insertTag = (tag: string) => {
    const textarea = document.querySelector('textarea');
    if (!textarea) return;

    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const selectedText = text.substring(start, end);

    const openTag = tag === 'a' ? '<a href="" title="">' : `<${tag}>`;
    const closeTag = `</${tag}>`;
    
    const newText = text.substring(0, start) + openTag + selectedText + closeTag + text.substring(end);
    setText(newText);
  };

  return (
    <form className="comment-form" onSubmit={handleSubmit}>
      <div className="form-group">
        <label>Имя пользователя *</label>
        <input
          type="text"
          value={userName}
          onChange={(e) => setUserName(e.target.value)}
          required
        />
        {errors.userName && <span className="error">{errors.userName}</span>}
      </div>

      <div className="form-group">
        <label>Email *</label>
        <input
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          required
        />
        {errors.email && <span className="error">{errors.email}</span>}
      </div>

      <div className="form-group">
        <label>Домашняя страница</label>
        <input
          type="url"
          value={homePage}
          onChange={(e) => setHomePage(e.target.value)}
          placeholder="https://example.com"
        />
        {errors.homePage && <span className="error">{errors.homePage}</span>}
      </div>

      <div className="form-group">
        <label>Текст комментария *</label>
        <div className="text-toolbar">
          <button type="button" onClick={() => insertTag('i')}>[i]</button>
          <button type="button" onClick={() => insertTag('strong')}>[strong]</button>
          <button type="button" onClick={() => insertTag('code')}>[code]</button>
          <button type="button" onClick={() => insertTag('a')}>[a]</button>
        </div>
        <textarea
          value={text}
          onChange={(e) => setText(e.target.value)}
          rows={5}
          required
        />
        {errors.text && <span className="error">{errors.text}</span>}
        <button type="button" onClick={handlePreview} style={{ marginTop: '10px' }}>
          Предпросмотр
        </button>
      </div>

      {preview && (
        <div className="preview">
          <h4>Предпросмотр:</h4>
          <div dangerouslySetInnerHTML={{ __html: preview }} />
        </div>
      )}

      <div className="form-group">
        <label>Изображение (JPG, PNG, GIF, макс 5MB)</label>
        <input
          type="file"
          accept="image/jpeg,image/jpg,image/png,image/gif"
          onChange={(e) => setImageFile(e.target.files?.[0] || null)}
        />
        {errors.imageFile && <span className="error">{errors.imageFile}</span>}
      </div>

      <div className="form-group">
        <label>Текстовый файл (.txt, макс 100KB)</label>
        <input
          type="file"
          accept=".txt,text/plain"
          onChange={(e) => setTextFile(e.target.files?.[0] || null)}
        />
        {errors.textFile && <span className="error">{errors.textFile}</span>}
      </div>

      <div className="form-group">
        <label>CAPTCHA *</label>
        <div style={{ 
          padding: '10px', 
          background: '#f0f0f0', 
          fontSize: '24px', 
          fontWeight: 'bold', 
          letterSpacing: '5px',
          userSelect: 'none'
        }}>
          {captchaImage}
        </div>
        <input
          type="text"
          value={captchaCode}
          onChange={(e) => setCaptchaCode(e.target.value)}
          placeholder="Введите код с картинки"
          required
        />
        {errors.captcha && <span className="error">{errors.captcha}</span>}
      </div>

      {errors.submit && <div className="error">{errors.submit}</div>}

      <button type="submit" className="submit-btn" disabled={uploading}>
        {uploading ? 'Отправка...' : 'Отправить'}
      </button>
    </form>
  );
};

export default CommentForm;