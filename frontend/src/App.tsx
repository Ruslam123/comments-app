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

const API_URL = process.env.REACT_APP_API_URL || window.location.origin;

console.log('🔧 API_URL:', API_URL);
console.log('🔧 Environment:', process.env.NODE_ENV);

function App() {
  const [comments, setComments] = useState<Comment[]>([]);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [sortBy, setSortBy] = useState('createdAt');
  const [ascending, setAscending] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [connectionStatus, setConnectionStatus] = useState<string>('connecting');

  // SignalR Connection
  useEffect(() => {
    let connection: signalR.HubConnection | null = null;

    const startConnection = async () => {
      try {
        console.log('🔌 Connecting to SignalR...');
        setConnectionStatus('connecting');

        connection = new signalR.HubConnectionBuilder()
          .withUrl(`${API_URL}/hubs/comments`, {
            skipNegotiation: false,
            withCredentials: true,
            transport: signalR.HttpTransportType.WebSockets | 
                       signalR.HttpTransportType.ServerSentEvents | 
                       signalR.HttpTransportType.LongPolling
          })
          .withAutomaticReconnect([0, 2000, 5000, 10000])
          .configureLogging(signalR.LogLevel.Information)
          .build();

        connection.onreconnecting(() => {
          console.log('🔄 SignalR reconnecting...');
          setConnectionStatus('reconnecting');
        });

        connection.onreconnected(() => {
          console.log('✅ SignalR reconnected');
          setConnectionStatus('connected');
        });

        connection.onclose(() => {
          console.log('❌ SignalR disconnected');
          setConnectionStatus('disconnected');
        });

        await connection.start();
        console.log('✅ SignalR Connected');
        setConnectionStatus('connected');

        connection.on('ReceiveComment', (comment: Comment) => {
          console.log('📩 Received new comment:', comment);
          
          if (!comment.parentCommentId && page === 1) {
            setComments(prev => [comment, ...prev].slice(0, 25));
          } else if (comment.parentCommentId) {
            setComments(prev => addReplyToComment(prev, comment));
          }
        });

      } catch (err) {
        console.error('❌ SignalR Error:', err);
        setConnectionStatus('error');
      }
    };

    startConnection();

    return () => {
      if (connection) {
        connection.stop().catch(err => console.error('SignalR disconnect error:', err));
      }
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
    
    const maxAttempts = 3;
    
    for (let attempt = 1; attempt <= maxAttempts; attempt++) {
      try {
        console.log(`🔄 Loading comments (attempt ${attempt}/${maxAttempts})...`);
        
        const url = `${API_URL}/api/comments?page=${page}&pageSize=25&sortBy=${sortBy}&ascending=${ascending}`;
        console.log(`📡 Fetching: ${url}`);
        
        const response = await fetch(url, {
          method: 'GET',
          headers: {
            'Accept': 'application/json',
            'Content-Type': 'application/json',
          },
          mode: 'cors',
          credentials: 'include'
        });
        
        console.log(`📊 Response status: ${response.status}`);
        
        if (!response.ok) {
          const errorText = await response.text();
          console.error(`❌ HTTP Error ${response.status}:`, errorText);
          throw new Error(`HTTP ${response.status}: ${errorText.substring(0, 100)}`);
        }
        
        const data: PagedResult = await response.json();
        console.log(`✅ Loaded ${data.items.length} comments (total: ${data.totalCount})`);
        
        setComments(data.items || []);
        setTotalPages(data.totalPages || 1);
        setLoading(false);
        setError(null);
        return;
        
      } catch (error) {
        console.error(`❌ Attempt ${attempt} failed:`, error);
        
        if (attempt === maxAttempts) {
          const errorMsg = error instanceof Error ? error.message : 'Unknown error';
          setError(`Не вдалося підключитися до API після ${maxAttempts} спроб.\n\nURL: ${API_URL}\nError: ${errorMsg}`);
          setLoading(false);
          setComments([]);
        } else {
          // Exponential backoff
          const delay = 1000 * Math.pow(2, attempt - 1);
          console.log(`⏳ Retrying in ${delay}ms...`);
          await new Promise(resolve => setTimeout(resolve, delay));
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
        <h1>💬 Комментарі</h1>
        <div style={{ fontSize: '0.9rem', marginTop: '10px' }}>
          <div>API: {API_URL}</div>
          <div style={{ 
            display: 'inline-block',
            padding: '4px 8px',
            borderRadius: '4px',
            marginTop: '5px',
            background: connectionStatus === 'connected' ? '#27ae60' : 
                       connectionStatus === 'connecting' ? '#f39c12' :
                       connectionStatus === 'reconnecting' ? '#e67e22' : '#e74c3c'
          }}>
            SignalR: {connectionStatus}
          </div>
        </div>
      </header>
      
      <main className="app-main">
        {error && (
          <div style={{
            padding: '20px',
            background: '#fee',
            color: '#c33',
            borderRadius: '8px',
            marginBottom: '20px',
            border: '2px solid #c33',
            whiteSpace: 'pre-wrap',
            fontFamily: 'monospace'
          }}>
            <strong>❌ Помилка підключення</strong>
            <div style={{ marginTop: '10px' }}>{error}</div>
            <button 
              onClick={loadComments}
              style={{
                marginTop: '15px',
                padding: '10px 20px',
                background: '#3498db',
                color: 'white',
                border: 'none',
                borderRadius: '4px',
                cursor: 'pointer'
              }}
            >
              🔄 Спробувати знову
            </button>
          </div>
        )}
        
        <CommentForm onCommentAdded={loadComments} />
        
        <div className="sort-controls">
          <button onClick={() => handleSort('userName')}>
            👤 Ім'я {sortBy === 'userName' && (ascending ? '↑' : '↓')}
          </button>
          <button onClick={() => handleSort('email')}>
            📧 Email {sortBy === 'email' && (ascending ? '↑' : '↓')}
          </button>
          <button onClick={() => handleSort('createdAt')}>
            📅 Дата {sortBy === 'createdAt' && (ascending ? '↑' : '↓')}
          </button>
        </div>

        {loading ? (
          <div className="loading">
            <div style={{ fontSize: '2rem' }}>⏳</div>
            <div>Завантаження...</div>
          </div>
        ) : comments.length === 0 ? (
          <div className="loading">
            <div style={{ fontSize: '2rem' }}>💬</div>
            <div>Немає коментарів. Будьте першим!</div>
          </div>
        ) : (
          <>
            <div style={{ 
              padding: '10px', 
              background: '#ecf0f1', 
              borderRadius: '4px',
              marginBottom: '15px',
              textAlign: 'center'
            }}>
              📊 Показано {comments.length} з {totalPages * 25} коментарів
            </div>
            <CommentList comments={comments} />
          </>
        )}

        {totalPages > 1 && (
          <div className="pagination">
            <button 
              onClick={() => setPage(p => Math.max(1, p - 1))} 
              disabled={page === 1 || loading}
            >
              ⬅️ Назад
            </button>
            <span style={{ fontWeight: 'bold' }}>
              Сторінка {page} / {totalPages}
            </span>
            <button 
              onClick={() => setPage(p => Math.min(totalPages, p + 1))} 
              disabled={page === totalPages || loading}
            >
              Вперед ➡️
            </button>
          </div>
        )}
      </main>
    </div>
  );
}

export default App;
