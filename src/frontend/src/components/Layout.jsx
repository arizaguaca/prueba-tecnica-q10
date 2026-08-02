import React from 'react';

export const Layout = ({ children }) => {
  return (
    <div className="layout">
      <header className="header">
        <div className="container header-container">
          <div className="logo-section">
            <span className="logo-icon">⚡</span>
            <h1 className="logo-text">OrderFlow</h1>
          </div>
          <nav className="nav">
            <span className="nav-badge">Dashboard</span>
          </nav>
        </div>
      </header>
      <main className="main-content">
        <div className="container">
          {children}
        </div>
      </main>
      <footer className="footer">
        <div className="container footer-container">
          <p>&copy; {new Date().getFullYear()} OrderFlow S.A. Todos los derechos reservados.</p>
        </div>
      </footer>
    </div>
  );
};
