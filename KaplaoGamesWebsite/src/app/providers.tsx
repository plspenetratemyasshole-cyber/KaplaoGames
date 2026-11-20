"use client";

import React, { useEffect, useState } from "react";
import { AntdRegistry } from "@ant-design/nextjs-registry";
import { ConfigProvider, theme } from "antd";

export default function Providers({ children }: { children: React.ReactNode }) {
  const [isDark, setIsDark] = useState(false);
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
    
    // Проверяем сохраненную тему или системную
    const savedTheme = localStorage.getItem("kaplao.theme");
    const prefersDark = savedTheme === "dark" || (!savedTheme && window.matchMedia("(prefers-color-scheme: dark)").matches);
    
    setIsDark(prefersDark);

    // Кастомное событие для изменения темы внутри компонентов
    const handleThemeChange = (e: CustomEvent) => {
      setIsDark(e.detail.isDark);
    };
    window.addEventListener("themeChange" as any, handleThemeChange);

    return () => {
      window.removeEventListener("themeChange" as any, handleThemeChange);
    };
  }, []);

  // Предотвращаем мерцание на первом рендере
  if (!mounted) {
    return (
      <AntdRegistry>
        <ConfigProvider
          theme={{
            algorithm: theme.defaultAlgorithm,
            token: {
              colorPrimary: "#2f6fed",
              borderRadius: 8,
              fontFamily:
                '"Segoe UI", system-ui, -apple-system, BlinkMacSystemFont, sans-serif',
              fontSize: 14,
              lineHeight: 1.5,
            },
            components: {
              Button: {
                controlHeight: 44,
                borderRadius: 8,
              },
              Input: {
                controlHeight: 44,
                borderRadius: 8,
              },
              Card: {
                paddingLG: 20,
                paddingMD: 16,
                borderRadius: 12,
              },
            },
          }}
        >
          {children}
        </ConfigProvider>
      </AntdRegistry>
    );
  }

  return (
    <AntdRegistry>
      <ConfigProvider
        theme={{
          algorithm: isDark ? theme.darkAlgorithm : theme.defaultAlgorithm,
          token: {
            colorPrimary: "#2f6fed",
            borderRadius: 8,
            fontFamily:
              '"Segoe UI", system-ui, -apple-system, BlinkMacSystemFont, sans-serif',
            fontSize: 14,
            lineHeight: 1.5,
            // Темная тема - не полностью черный
            ...(isDark && {
              colorBgContainer: "#1a1d26",
              colorBgElevated: "#252830",
              colorBorder: "rgba(255, 255, 255, 0.12)",
              colorText: "#e8e9ed",
              colorTextSecondary: "rgba(232, 233, 237, 0.65)",
            }),
          },
          components: {
            Button: {
              controlHeight: 44,
              borderRadius: 8,
            },
            Input: {
              controlHeight: 44,
              borderRadius: 8,
            },
            Card: {
              paddingLG: 20,
              paddingMD: 16,
              borderRadius: 12,
            },
          },
        }}
      >
        {children}
      </ConfigProvider>
    </AntdRegistry>
  );
}
