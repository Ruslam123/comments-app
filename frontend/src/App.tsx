import React, { useState, useEffect } from 'react';
import * as signalR from '@microsoft/signalr';
import CommentList from './components/CommentList';
import CommentForm from './components/CommentForm';
import './App.css';

interface Comment {
  id: string;
  userName: string;
  email: string;
  homePage?: string;
  text: string;
  imageUrl?: string;
  textFileUrl?: string;
  createdAt: string;
  parentCommentId?: string;
  replies: Comment[];
}

interface PagedResult {
  items: Comment[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

const API_URL = process.env.REACT_APP_API_URL || 'https://lovely-achievement-production.up.railway.app';

console.log('API_URL:', API_URL);

function App() {
  const [comments, setComments] = useState<Comment[]>([]);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [sortBy, setSortBy] = useState('createdAt');
  const [ascending, setAscending] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_URL}/hubs/comments`, {
        skipNegotiation: false,
        transport: signalR.HttpTransportType.WebSockets | 
                   signalR.HttpTransportType.ServerSentEvents | 
                   signalR.HttpTransportType.LongPolling
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    connection.start()
      .then(() => {
        console.log('SignalR Connected');
        connection.on('ReceiveComment', (comment: Comment) => {
          console.log('Received new comment:', comment);
          
          if (!comment.parentCommentId && page === 1) {
            setComments(prev => [comment, ...prev].slice(0, 25));
          } else if (comment.parentCommentId) {
            setComments(prev => addReplyToComment(prev, comment));
          }
        });
      })
      .catch(err => {
        console.error('SignalR Error:', err);
      });

    return () => {
      connection.stop().catch(err => console.error('SignalR disconnect error:', err));
    };
  }, [page]);

  const addReplyToComment = (comments: Comment[], reply: Comment): Comment[] => {
    return comments.map(comment => {
      if (comment.id === reply.parentCommentId) {
        return {
          ...comment,
          replies: [...comment.replies, reply]
        };
      }
      if (comment.replies.length > 0) {
        return {
          ...comment,
          replies: addReplyToComment(comment.replies, reply)
        };
      }
      return comment;
    });
  };

  useEffect(() => {
    loadComments();
  }, [page, sortBy, ascending]);

  const loadComments = async () => {
    setLoading(true);
    setError(null);
    
    for (let attempt = 1; attempt <= 3; attempt++) {
      try {
        console.log(`Loading comments (attempt ${attempt})...`);
        
        const response = await fetch(
          `${API_URL}/api/comments?page=${page}&pageSize=25&sortBy=${sortBy}&ascending=${ascending}`,
          {
            method: 'GET',
            headers: {
              'Accept': 'application/json',
            },
            mode: 'cors',
          }
        );
        
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`);
        }
        
        const data: PagedResult = await response.json();
        setComments(data.items);
        setTotalPages(data.totalPages);
        setLoading(false);
        return;
        
      } catch (error) {
        console.error(`Attempt ${attempt} failed:`, error);
        
        if (attempt === 3) {
          setError(`Не вдалося підключитися до API після 3 спроб. URL: ${API_URL}`);
          setLoading(false);
        } else {
          await new Promise(resolve => setTimeout(resolve, 1000 * attempt));
        }
      }
    }
  };

  const handleSort = (field: string) => {
    if (sortBy === field) {
      setAscending(!ascending);
    } else {
      setSortBy(field);
      setAscending(false);
    }
  };

  return (
    <div className="app">
      <header className="app-header">
        <h1>Комментарии</h1>
        <small>API: {API_URL}</small>
      </header>
      
      <main className="app-main">
        {error && (
          <div style={{
            padding: '15px',
            background: '#fee',
            color: '#c33',
            borderRadius: '5px',
            marginBottom: '20px'
          }}>
            <strong>Помилка:</strong> {error}
          </div>
        )}
        
        <CommentForm onCommentAdded={loadComments} />
        
        <div className="sort-controls">
          <button onClick={() => handleSort('userName')}>
            Сортувати по імені {sortBy === 'userName' && (ascending ? '↑' : '↓')}
          </button>
          <button onClick={() => handleSort('email')}>
            Сортувати по email {sortBy === 'email' && (ascending ? '↑' : '↓')}
          </button>
          <button onClick={() => handleSort('createdAt')}>
            Сортувати по даті {sortBy === 'createdAt' && (ascending ? '↑' : '↓')}
          </button>
        </div>

        {loading ? (
          <div className="loading">Завантаження...</div>
        ) : comments.length === 0 ? (
          <div className="loading">Немає коментарів</div>
        ) : (
          <CommentList comments={comments} />
        )}

        <div className="pagination">
          <button 
            onClick={() => setPage(p => Math.max(1, p - 1))} 
            disabled={page === 1}
          >
            Назад
          </button>
          <span>Сторінка {page} з {totalPages}</span>
          <button 
            onClick={() => setPage(p => Math.min(totalPages, p + 1))} 
            disabled={page === totalPages}
          >
            Вперед
          </button>
        </div>
      </main>
    </div>
  );
}

export default App;