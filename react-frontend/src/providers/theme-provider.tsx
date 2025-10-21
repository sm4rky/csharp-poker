import React, { createContext, useContext, useEffect, useState } from "react";

export type ThemeMode = "light" | "dark" | "forest" | "ocean" | "retro";

export const THEMES: ThemeMode[] = [
  "light",
  "dark",
  "forest",
  "ocean",
  "retro",
];

interface ThemeContextValue {
  theme: ThemeMode;
  setTheme: (theme: ThemeMode) => void;
}

const ThemeContext = createContext<ThemeContextValue | undefined>(undefined);

interface ThemeProviderProps {
  children: React.ReactNode;
  defaultTheme?: ThemeMode;
  storageKey?: string;
}

export default function ThemeProvider({
  children,
  defaultTheme = "light",
  storageKey = "app-theme",
}: ThemeProviderProps) {
  const [theme, setTheme] = useState<ThemeMode>(() => {
    try {
      const stored = localStorage.getItem(storageKey) as ThemeMode | null;
      return stored ?? defaultTheme;
    } catch {
      return defaultTheme;
    }
  });

  useEffect(() => {
    const root = document.documentElement;
    root.classList.remove("light", "dark", "forest", "ocean", "retro");
    root.classList.add(theme);

    try {
      localStorage.setItem(storageKey, theme);
    } catch {
      /* ignore */
    }
  }, [theme, storageKey]);

  return (
    <ThemeContext.Provider value={{ theme, setTheme }}>
      {children}
    </ThemeContext.Provider>
  );
}

export function useTheme() {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error("useTheme must be used within ThemeProvider");
  return ctx;
}
