import React, { useState, useEffect } from 'react';
import './CommentForm.css';

const API_URL = process.env.REACT_APP_API_URL || 'https://lovely-achievement-production.up.railway.app';

interface CommentFormProps {
  onCommentAdded: () => void;
  parentId?: string;
}
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
  const [imagePreview, setImagePreview] = useState<string>('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    loadCaptcha();
  }, []);

  const loadCaptcha = async () => {
    try {
      const response = await fetch(`${API_URL}/api/captcha`);
      if (!response.ok) throw new Error('Failed to load captcha');
      const data = await response.json();
      setCaptchaToken(data.token);
      setCaptchaImage(data.code);
    } catch (error) {
      console.error('Error loading captcha:', error);
    }
  };

  const handleImageChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      if (!file.type.match('image/(jpeg|jpg|png|gif)')) {
        setErrors(prev => ({ ...prev, image: 'Дозволені тільки JPG, PNG, GIF' }));
        return;
      }
      if (file.size > 5 * 1024 * 1024) {
        setErrors(prev => ({ ...prev, image: 'Файл занадто великий (макс. 5MB)' }));
        return;
      }
      setImageFile(file);
      setErrors(prev => ({ ...prev, image: '' }));
      
      const reader = new FileReader();
      reader.onloadend = () => setImagePreview(reader.result as string);
      reader.readAsDataURL(file);
    }
  };

  const handleTextFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      if (!file.name.endsWith('.txt')) {
        setErrors(prev => ({ ...prev, textFile: 'Дозволені тільки .txt файли' }));
        return;
      }
      if (file.size > 100 * 1024) {
        setErrors(prev => ({ ...prev, textFile: 'Файл занадто великий (макс. 100KB)' }));
        return;
      }
      setTextFile(file);
      setErrors(prev => ({ ...prev, textFile: '' }));
    }
  };

  const validateForm = (): boolean => {
    const newErrors: Record<string, string> = {};
    
    if (!userName.trim()) {
      newErrors.userName = "Ім'я обов'язкове";
    } else if (!/^[a-zA-Z0-9]+$/.test(userName)) {
      newErrors.userName = 'Тільки латинські літери та цифри';
    }
    
    if (!email.trim()) {
      newErrors.email = 'Email обов\'язковий';
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      newErrors.email = 'Невірний формат email';
    }
    
    if (homePage && !/^https?:\/\/.+/.test(homePage)) {
      newErrors.homePage = 'Невірний формат URL (має починатися з http:// або https://)';
    }
    
    if (!text.trim()) {
      newErrors.text = 'Текст обов\'язковий';
    }
    
    if (!captchaCode.trim()) {
      newErrors.captcha = 'Введіть CAPTCHA';
    }
    
    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handlePreview = async () => {
    try {
      const response = await fetch('/api/comments/preview', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ text })
      });
      const data = await response.json();
      setPreview(data.html);
    } catch (error) {
      console.error('Preview error:', error);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (isSubmitting) return;
    
    if (!validateForm()) {
      console.log('Validation failed:', errors);
      return;
    }

    setIsSubmitting(true);

    try {
      // Перевірка CAPTCHA
      console.log('Validating captcha...');
      const captchaValid = await fetch('/api/captcha/validate', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token: captchaToken, code: captchaCode })
      });
      
      if (!captchaValid.ok) {
        throw new Error('CAPTCHA validation failed');
      }
      
      const captchaResult = await captchaValid.json();

      if (!captchaResult.valid) {
        setErrors({ captcha: 'Невірний код CAPTCHA' });
        loadCaptcha();
        setCaptchaCode('');
        setIsSubmitting(false);
        return;
      }

      console.log('Captcha validated successfully');

      let imageUrl = null;
      let textFileUrl = null;

      // Завантаження зображення
      if (imageFile) {
        console.log('Uploading image...');
        const formData = new FormData();
        formData.append('file', imageFile);
        
        const imgResponse = await fetch('/api/file/image', {
          method: 'POST',
          body: formData
        });
        
        if (!imgResponse.ok) {
          const errorData = await imgResponse.json();
          throw new Error(errorData.error || 'Помилка завантаження зображення');
        }
        
        const imgData = await imgResponse.json();
        imageUrl = imgData.url;
        console.log('Image uploaded:', imageUrl);
      }

      // Завантаження текстового файлу
      if (textFile) {
        console.log('Uploading text file...');
        const formData = new FormData();
        formData.append('file', textFile);
        
        const txtResponse = await fetch('/api/file/text', {
          method: 'POST',
          body: formData
        });
        
        if (!txtResponse.ok) {
          const errorData = await txtResponse.json();
          throw new Error(errorData.error || 'Помилка завантаження текстового файлу');
        }
        
        const txtData = await txtResponse.json();
        textFileUrl = txtData.url;
        console.log('Text file uploaded:', textFileUrl);
      }

      // Створення коментаря
      console.log('Creating comment...');
      const commentData = {
        userName,
        email,
        homePage: homePage || null,
        text,
        captchaToken,
        parentCommentId: parentId || null,
        imagePath: imageUrl,
        textFilePath: textFileUrl
      };
      
      console.log('Comment data:', commentData);

      const response = await fetch('/api/comments', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(commentData)
      });

      if (!response.ok) {
        const errorText = await response.text();
        console.error('Error response:', errorText);
        throw new Error(`Помилка створення коментаря: ${response.status}`);
      }

      console.log('Comment created successfully');

      // Очищення форми
      setUserName('');
      setEmail('');
      setHomePage('');
      setText('');
      setCaptchaCode('');
      setPreview('');
      setImageFile(null);
      setTextFile(null);
      setImagePreview('');
      loadCaptcha();
      onCommentAdded();

    } catch (error) {
      console.error('Submit error:', error);
      setErrors({ submit: error instanceof Error ? error.message : 'Помилка відправки' });
    } finally {
      setIsSubmitting(false);
    }
  };

  const insertTag = (tag: string) => {
    const openTag = tag === 'a' ? '<a href="" title="">' : `<${tag}>`;
    const closeTag = `</${tag}>`;
    setText(prev => prev + openTag + closeTag);
  };

  return (
    <form className="comment-form" onSubmit={handleSubmit}>
      {errors.submit && (
        <div style={{ 
          padding: '10px', 
          background: '#fee', 
          color: '#c33', 
          borderRadius: '4px',
          marginBottom: '15px' 
        }}>
          {errors.submit}
        </div>
      )}

      <div className="form-group">
        <label>Ім'я користувача *</label>
        <input
          type="text"
          value={userName}
          onChange={(e) => setUserName(e.target.value)}
          required
          disabled={isSubmitting}
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
          disabled={isSubmitting}
        />
        {errors.email && <span className="error">{errors.email}</span>}
      </div>

      <div className="form-group">
        <label>Домашня сторінка</label>
        <input
          type="url"
          value={homePage}
          onChange={(e) => setHomePage(e.target.value)}
          placeholder="https://example.com"
          disabled={isSubmitting}
        />
        {errors.homePage && <span className="error">{errors.homePage}</span>}
      </div>

      <div className="form-group">
        <label>Текст коментаря *</label>
        <div className="text-toolbar">
          <button type="button" onClick={() => insertTag('i')} disabled={isSubmitting}>[i]</button>
          <button type="button" onClick={() => insertTag('strong')} disabled={isSubmitting}>[strong]</button>
          <button type="button" onClick={() => insertTag('code')} disabled={isSubmitting}>[code]</button>
          <button type="button" onClick={() => insertTag('a')} disabled={isSubmitting}>[a]</button>
        </div>
        <textarea
          value={text}
          onChange={(e) => setText(e.target.value)}
          rows={5}
          required
          disabled={isSubmitting}
        />
        {errors.text && <span className="error">{errors.text}</span>}
        <button type="button" onClick={handlePreview} disabled={isSubmitting}>
          Попередній перегляд
        </button>
      </div>

      {preview && (
        <div className="preview">
          <h4>Попередній перегляд:</h4>
          <div dangerouslySetInnerHTML={{ __html: preview }} />
        </div>
      )}

      <div className="form-group">
        <label>Зображення (JPG, PNG, GIF - макс. 320x240)</label>
        <input
          type="file"
          accept="image/jpeg,image/jpg,image/png,image/gif"
          onChange={handleImageChange}
          disabled={isSubmitting}
        />
        {errors.image && <span className="error">{errors.image}</span>}
        {imagePreview && (
          <div style={{ marginTop: '10px' }}>
            <img
              src={imagePreview}
              alt="Preview"
              style={{
                maxWidth: '320px',
                maxHeight: '240px',
                border: '1px solid #ddd',
                borderRadius: '4px'
              }}
            />
          </div>
        )}
      </div>

      <div className="form-group">
        <label>Текстовий файл (TXT - макс. 100KB)</label>
        <input
          type="file"
          accept=".txt"
          onChange={handleTextFileChange}
          disabled={isSubmitting}
        />
        {errors.textFile && <span className="error">{errors.textFile}</span>}
        {textFile && (
          <div style={{ marginTop: '5px', color: '#27ae60' }}>
            ✓ {textFile.name}
          </div>
        )}
      </div>

      <div className="form-group">
        <label>CAPTCHA *</label>
        <div style={{
          padding: '10px',
          background: '#f0f0f0',
          fontSize: '24px',
          fontWeight: 'bold',
          letterSpacing: '5px',
          marginBottom: '10px'
        }}>
          {captchaImage}
        </div>
        <input
          type="text"
          value={captchaCode}
          onChange={(e) => setCaptchaCode(e.target.value)}
          required
          disabled={isSubmitting}
          placeholder="Введіть код з картинки"
        />
        {errors.captcha && <span className="error">{errors.captcha}</span>}
      </div>

      <button type="submit" className="submit-btn" disabled={isSubmitting}>
        {isSubmitting ? 'Відправка...' : 'Відправити'}
      </button>
    </form>
  );
};

export default CommentForm;