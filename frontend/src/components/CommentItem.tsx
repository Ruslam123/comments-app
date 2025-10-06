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
            {comment.homePage}
          </a>
        )}
        <span className="date">{new Date(comment.createdAt).toLocaleString()}</span>
      </div>
      
      <div className="comment-body" dangerouslySetInnerHTML={{ __html: comment.text }} />
      
      {comment.imageUrl && (
        <div className="comment-image" onClick={() => setLightboxImage(comment.imageUrl!)}>
          <img
            src={comment.imageUrl}
            alt="Comment attachment"
            style={{
              maxWidth: '320px',
              maxHeight: '240px',
              cursor: 'pointer',
              borderRadius: '4px',
              marginTop: '10px',
              border: '1px solid #ddd'
            }}
          />
        </div>
      )}
      
      {comment.textFileUrl && (
        <div className="comment-file" style={{ marginTop: '10px' }}>
          <a
            href={comment.textFileUrl}
            target="_blank"
            rel="noopener noreferrer"
            style={{
              color: '#3498db',
              textDecoration: 'none',
              display: 'inline-block',
              padding: '8px 12px',
              background: '#ecf0f1',
              borderRadius: '4px'
            }}
          >
            📄 Переглянути текстовий файл
          </a>
        </div>
      )}
      
      <button onClick={() => setShowReplyForm(!showReplyForm)}>Відповісти</button>
      
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