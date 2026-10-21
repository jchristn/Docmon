import { createContext, useContext, useState } from "react";
const ThemeContext = createContext(null);
export function ThemeProvider({ children }) {
  const [theme, setTheme] = useState(
    () => localStorage.getItem("docmon.theme") || "dark",
  );
  document.documentElement.dataset.theme = theme;
  const toggle = () => {
    const next = theme === "dark" ? "light" : "dark";
    localStorage.setItem("docmon.theme", next);
    setTheme(next);
  };
  return (
    <ThemeContext.Provider value={{ theme, toggle }}>
      {children}
    </ThemeContext.Provider>
  );
}
export const useTheme = () => useContext(ThemeContext);
