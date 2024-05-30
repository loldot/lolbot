import { useState } from 'react'
import Chessboard from './components/Chessboard'

function App() {
  const [count, setCount] = useState(0)

  return (<Chessboard />)
}

export default App
