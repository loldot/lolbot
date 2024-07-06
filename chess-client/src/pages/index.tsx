import { Link, useNavigate } from "react-router-dom"

const Home = () => {
    const navigate = useNavigate();
    return (
        <div>
            <button onClick={() => navigate("/game/new")}>New Game</button>
            <button onClick={() => navigate("/game/0")}>Watch a master Game</button>
        </div>
    );
}

export default Home;