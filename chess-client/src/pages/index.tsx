import { Link } from "react-router-dom"

const Home = () => (
    <p>
        <Link to={"/game/new"}>New Game</Link>
        <Link to={"/game/0"}>Master Game</Link>
    </p>
);

export default Home;