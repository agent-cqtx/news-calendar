import { Link, Route, Routes } from 'react-router-dom'
import { DayPage } from './pages/DayPage'
import { MonthPage } from './pages/MonthPage'
import { PostPage } from './pages/PostPage'
import './App.css'

export default function App() {
  return (
    <main className="app">
      <header className="top"><h1><Link to="/">News Calendar</Link></h1><Link to="/post">Post test</Link></header>
      <Routes>
        <Route path="/" element={<MonthPage />} />
        <Route path="/post" element={<PostPage />} />
        <Route path="/day/:date" element={<DayPage />} />
        <Route path="*" element={<p>Not found. <Link to="/">Home</Link></p>} />
      </Routes>
    </main>
  )
}
