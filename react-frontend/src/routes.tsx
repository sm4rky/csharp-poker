import { createBrowserRouter } from "react-router-dom";
import GamePage from "pages/game";
import LobbyPage from "pages/lobby";

const AppRoutes = createBrowserRouter([
  {
    path: "/",
    children: [
      { index: true, element: <LobbyPage /> },
      { path: "game", element: <GamePage /> },
    ],
  },
]);

export default AppRoutes;
