import { Link, Route, Routes, useLocation } from 'react-router-dom'
import { AllPage } from './pages/AllPage'
import { DayPage } from './pages/DayPage'
import { MonthPage } from './pages/MonthPage'
import { PostPage } from './pages/PostPage'
import { ThemeToggle } from './components/ThemeToggle'
import './App.css'

export default function App() {
  const { pathname } = useLocation()
  return (
    <main className="app">
      <header className="top">
        <h1><Link to="/">News Calendar</Link></h1>
        <nav className="top-nav" aria-label="Main">
          <Link to="/all" className={`nav-link${pathname === '/all' ? ' active' : ''}`}>All</Link>
          <ThemeToggle />
        </nav>
      </header>
      <Routes>
        <Route path="/" element={<MonthPage />} />
        <Route path="/all" element={<AllPage />} />
        <Route path="/post" element={<PostPage />} />
        <Route path="/day/:date" element={<DayPage />} />
        <Route path="*" element={<p>Not found. <Link to="/">Home</Link></p>} />
      </Routes>
    </main>
  )
}
