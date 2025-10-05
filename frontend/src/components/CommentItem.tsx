import React, { useState } from 'react';
import CommentForm from './CommentForm';
import Lightbox from './Lightbox';
import './CommentItem.css';

interface Comment {
  id: string;
  userName: string;
  email: string;
  homePage?: string;
  text: string;
  imageUrl?: string;
  textFileUrl?: string;
  createdAt: string;
  replies: Comment[];
}

interface CommentItemProps {
  comment: Comment;
}

const API_URL = process.env.REACT_APP_API_URL || 'http://localhost:5000';

const CommentItem: React.FC<CommentItemProps> = ({ comment }) => {
  const [showReplyForm, setShowReplyForm] = useState(false);
  const [lightboxImage, setLightboxImage] = useState<string | null>(null);

  return (
    <div className="comment-item">
      <div className="comment-header">
        <strong>{comment.userName}</strong>
        <span className="email">{comment.email}</span>
        {comment.homePage && (
          <a href={comment.homePage} target="_blank" rel="noopener noreferrer">
            🔗 {comment.homePage}
          </a>
        )}
        <span className="date">
          {new Date(comment.createdAt).toLocaleString('ru-RU')}
        </span>
      </div>
      
      <div className="comment-body" dangerouslySetInnerHTML={{ __html: comment.text }} />
      
      {comment.imageUrl && (
        <div className="comment-attachments">
          <img 
            src={`${API_URL}${comment.imageUrl}`}
            alt="Attachment"
            className="comment-image"
            onClick={() => setLightboxImage(`${API_URL}${comment.imageUrl}`)}
            style={{ cursor: 'pointer' }}
          />
        </div>
      )}
      
      {comment.textFileUrl && (
        <div className="comment-attachments">
          <a 
            href={`${API_URL}${comment.textFileUrl}`}
            target="_blank"
            rel="noopener noreferrer"
            className="text-file-link"
          >
            📄 Текстовый файл
          </a>
        </div>
      )}
      
      <button onClick={() => setShowReplyForm(!showReplyForm)}>
        💬 Ответить
      </button>
      
      {showReplyForm && (
        <CommentForm 
          parentId={comment.id} 
          onCommentAdded={() => setShowReplyForm(false)} 
        />
      )}
      
      {comment.replies.length > 0 && (
        <div className="comment-replies">
          {comment.replies.map(reply => (
            <CommentItem key={reply.id} comment={reply} />
          ))}
        </div>
      )}
      
      {lightboxImage && (
        <Lightbox 
          imageUrl={lightboxImage}
          onClose={() => setLightboxImage(null)}
        />
      )}
    </div>
  );
};

export default CommentItem;