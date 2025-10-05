import React, { useState, useEffect } from 'react';
import './CommentForm.css';

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

  useEffect(() => {
    loadCaptcha();
  }, []);

  const loadCaptcha = async () => {
  const response = await fetch('http://localhost:5000/api/captcha');
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

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handlePreview = async () => {
    const response = await fetch('http://localhost:5000/api/comments/preview', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ text })
    });
    const data = await response.json();
    setPreview(data.html);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!validateForm()) return;
console.log('Sending data:', {
    userName,
    email,
    homePage: homePage || null,
    text,
    captchaToken,
    parentCommentId: parentId || null
  });

    const captchaValid = await fetch('http://localhost:5000/api/captcha/validate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ token: captchaToken, code: captchaCode })
    });
    const captchaResult = await captchaValid.json();

    if (!captchaResult.valid) {
      setErrors({ captcha: 'Неверный код CAPTCHA' });
      loadCaptcha();
      return;
    }

    const response = await fetch('http://localhost:5000/api/comments', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        userName,
        email,
        homePage: homePage || null,
        text,
        captchaToken,
        parentCommentId: parentId || null
      })
    });

    if (response.ok) {
      setUserName('');
      setEmail('');
      setHomePage('');
      setText('');
      setCaptchaCode('');
      setPreview('');
      loadCaptcha();
      onCommentAdded();
    }
  };

  const insertTag = (tag: string) => {
    const openTag = tag === 'a' ? '<a href="" title="">' : `<${tag}>`;
    const closeTag = `</${tag}>`;
    setText(prev => prev + openTag + closeTag);
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
        <button type="button" onClick={handlePreview}>Предпросмотр</button>
      </div>

      {preview && (
        <div className="preview">
          <h4>Предпросмотр:</h4>
          <div dangerouslySetInnerHTML={{ __html: preview }} />
        </div>
      )}

      <div className="form-group">
  <label>CAPTCHA *</label>
  <div style={{ padding: '10px', background: '#f0f0f0', fontSize: '24px', fontWeight: 'bold', letterSpacing: '5px' }}>
    {captchaImage}
  </div>
  <input
    type="text"
    value={captchaCode}
    onChange={(e) => setCaptchaCode(e.target.value)}
    required
  />
  {errors.captcha && <span className="error">{errors.captcha}</span>}
</div>

      <button type="submit" className="submit-btn">Отправить</button>
    </form>
  );
};

export default CommentForm;