import {
  createContext,
  useContext,
  useState,
  useMemo,
  useCallback,
} from "react";
import { ApiClient } from "../utils/api";
const AuthContext = createContext(null);
export function AuthProvider({ children }) {
  const [token, setToken] = useState(
    () => sessionStorage.getItem("docmon.token") || "",
  );
  const logout = useCallback(() => {
    sessionStorage.removeItem("docmon.token");
    setToken("");
  }, []);
  const api = useMemo(() => new ApiClient(token, logout), [token, logout]);
  async function login(candidate) {
    await new ApiClient(candidate).get("system/info");
    sessionStorage.setItem("docmon.token", candidate);
    setToken(candidate);
  }
  return (
    <AuthContext.Provider value={{ token, api, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}
export const useAuth = () => useContext(AuthContext);
