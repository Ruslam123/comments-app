import React, { useState } from 'react';
import CommentForm from './CommentForm';
import './CommentItem.css';

interface Comment {
  id: string;
  userName: string;
  email: string;
  homePage?: string;
  text: string;
  createdAt: string;
  replies: Comment[];
}

interface CommentItemProps {
  comment: Comment;
}

const CommentItem: React.FC<CommentItemProps> = ({ comment }) => {
  const [showReplyForm, setShowReplyForm] = useState(false);

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
      <button onClick={() => setShowReplyForm(!showReplyForm)}>Ответить</button>
      
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
    </div>
  );
};

export default CommentItem;