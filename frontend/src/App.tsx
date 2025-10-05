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
  replies: Comment[];
}

interface PagedResult {
  items: Comment[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

const API_URL = process.env.REACT_APP_API_URL || 'http://localhost:5000';

function App() {
  const [comments, setComments] = useState<Comment[]>([]);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [sortBy, setSortBy] = useState('createdAt');
  const [ascending, setAscending] = useState(false);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_URL}/hubs/comments`)
      .withAutomaticReconnect()
      .build();

    connection.start()
      .then(() => {
        console.log('SignalR підключено');
        connection.on('ReceiveComment', (comment: Comment) => {
          console.log('Новий коментар через SignalR:', comment);
          setComments(prev => [comment, ...prev]);
        });
      })
      .catch(err => console.error('SignalR помилка:', err));

    return () => {
      connection.stop();
    };
  }, []);

  useEffect(() => {
    loadComments();
  }, [page, sortBy, ascending]);

  const loadComments = async () => {
    setLoading(true);
    try {
      const response = await fetch(
        `${API_URL}/api/comments?page=${page}&pageSize=25&sortBy=${sortBy}&ascending=${ascending}`
      );
      const data: PagedResult = await response.json();
      setComments(data.items);
      setTotalPages(data.totalPages);
    } catch (error) {
      console.error('Помилка завантаження коментарів:', error);
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
        <h1>💬 Комментарии</h1>
      </header>
      
      <main className="app-main">
        <CommentForm onCommentAdded={loadComments} />
        
        <div className="sort-controls">
          <button onClick={() => handleSort('userName')}>
            Сортировать по имени {sortBy === 'userName' && (ascending ? '↑' : '↓')}
          </button>
          <button onClick={() => handleSort('email')}>
            Сортировать по email {sortBy === 'email' && (ascending ? '↑' : '↓')}
          </button>
          <button onClick={() => handleSort('createdAt')}>
            Сортировать по дате {sortBy === 'createdAt' && (ascending ? '↑' : '↓')}
          </button>
        </div>

        {loading ? (
          <div className="loading">Загрузка...</div>
        ) : (
          <CommentList comments={comments} />
        )}

        <div className="pagination">
          <button 
            onClick={() => setPage(p => Math.max(1, p - 1))} 
            disabled={page === 1}
          >
            ← Назад
          </button>
          <span>Страница {page} из {totalPages}</span>
          <button 
            onClick={() => setPage(p => Math.min(totalPages, p + 1))} 
            disabled={page === totalPages}
          >
            Вперед →
          </button>
        </div>
      </main>
    </div>
  );
}

export default App;