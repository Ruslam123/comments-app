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

// ВАЖЛИВО: Отримуємо API URL з environment
const API_URL = process.env.REACT_APP_API_URL || 'http://localhost:5000';

console.log('API_URL:', API_URL); // Для дебагу

function App() {
  const [comments, setComments] = useState<Comment[]>([]);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [sortBy, setSortBy] = useState('createdAt');
  const [ascending, setAscending] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    // SignalR з обробкою помилок
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_URL}/hubs/comments`, {
        skipNegotiation: false,
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.ServerSentEvents | signalR.HttpTransportType.LongPolling
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
        // Не блокуємо застосунок якщо SignalR не працює
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
    
    try {
      console.log('Loading comments from:', `${API_URL}/api/comments`);
      
      const response = await fetch(
        `${API_URL}/api/comments?page=${page}&pageSize=25&sortBy=${sortBy}&ascending=${ascending}`,
        {
          headers: {
            'Accept': 'application/json',
            'Content-Type': 'application/json'
          }
        }
      );
      
      if (!response.ok) {
        const text = await response.text();
        console.error('Response error:', text);
        throw new Error(`HTTP ${response.status}: ${text.substring(0, 100)}`);
      }
      
      const contentType = response.headers.get('content-type');
      if (!contentType || !contentType.includes('application/json')) {
        const text = await response.text();
        console.error('Invalid content-type:', contentType);
        console.error('Response:', text.substring(0, 200));
        throw new Error('Server returned HTML instead of JSON. Check API URL.');
      }
      
      const data: PagedResult = await response.json();
      setComments(data.items);
      setTotalPages(data.totalPages);
    } catch (error) {
      console.error('Error loading comments:', error);
      setError(error instanceof Error ? error.message : 'Unknown error');
    } finally {
      setLoading(false);
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