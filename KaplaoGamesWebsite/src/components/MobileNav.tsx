"use client";

import { useState, useEffect } from "react";
import Link from "next/link";
import { Menu, Button, Switch } from "antd";
import { MenuOutlined, CloseOutlined, HomeOutlined, UserOutlined, LoginOutlined, LogoutOutlined, BulbOutlined } from "@ant-design/icons";
import styles from "./MobileNav.module.css";

type MobileNavProps = {
  isSignedIn: boolean;
  nickname?: string;
  isSuperAdmin?: boolean;
  onSignOut?: () => void;
};

export default function MobileNav({ isSignedIn, nickname, isSuperAdmin, onSignOut }: MobileNavProps) {
  const [isMenuOpen, setIsMenuOpen] = useState(false);
  const [isDark, setIsDark] = useState(false);

  useEffect(() => {
    // Загружаем тему из localStorage
    const savedTheme = localStorage.getItem("kaplao.theme");
    const prefersDark = savedTheme === "dark" || (!savedTheme && window.matchMedia("(prefers-color-scheme: dark)").matches);
    setIsDark(prefersDark);
  }, []);

  const toggleMenu = () => {
    setIsMenuOpen(!isMenuOpen);
  };

  const closeMenu = () => {
    setIsMenuOpen(false);
  };

  const toggleTheme = (checked: boolean) => {
    const newTheme = checked ? "dark" : "light";
    
    // Сохраняем в localStorage
    localStorage.setItem("kaplao.theme", newTheme);
    
    // Обновляем DOM напрямую
    document.documentElement.setAttribute("data-theme", newTheme);
    if (checked) {
      document.documentElement.classList.add("dark-theme");
    } else {
      document.documentElement.classList.remove("dark-theme");
    }
    
    // Обновляем локальное состояние
    setIsDark(checked);
    
    // Отправляем кастомное событие для providers
    window.dispatchEvent(new CustomEvent("themeChange", { detail: { isDark: checked } }));
  };

  return (
    <>
      {/* Header for all screens */}
      <div className={styles.header}>
        <div className={styles.headerContent}>
          <Link href="/" className={styles.logo}>
            <span className={styles.logoText}>Kaplao</span>
          </Link>
          <Button
            type="text"
            size="large"
            onClick={toggleMenu}
            icon={isMenuOpen ? <CloseOutlined /> : <MenuOutlined />}
            className={styles.menuButton}
          />
        </div>
      </div>

      {/* Menu drawer */}
      {isMenuOpen && (
        <div className={styles.menu}>
          <Menu
            mode="vertical"
            className={styles.menuList}
            items={[
              {
                key: "home",
                icon: <HomeOutlined />,
                label: <Link href="/">Home</Link>,
                onClick: closeMenu,
              },
              ...(isSignedIn
                ? [
                    {
                      key: "account-section",
                      label: `${nickname}'s Account`,
                      type: "group" as const,
                    },
                    {
                      key: "account",
                      icon: <UserOutlined />,
                      label: <Link href="/account">Profile</Link>,
                      onClick: closeMenu,
                    },
                    {
                      key: "change-nickname",
                      label: <Link href="/account/change-nickname">Change Nickname</Link>,
                      onClick: closeMenu,
                    },
                    {
                      key: "change-password",
                      label: <Link href="/account/change-password">Change Password</Link>,
                      onClick: closeMenu,
                    },
                    {
                      key: "contact-dev",
                      label: <Link href="/account/contact-dev">Contact Developer</Link>,
                      onClick: closeMenu,
                    },
                    {
                      key: "delete-divider",
                      type: "divider" as const,
                    },
                    {
                      key: "delete-account",
                      danger: true,
                      label: <Link href="/account">Delete Account</Link>,
                      onClick: closeMenu,
                    },
                    ...(isSuperAdmin
                      ? [
                          {
                            key: "admin-divider",
                            type: "divider" as const,
                          },
                          {
                            key: "admin",
                            label: <Link href="/admin">🔑 Admin Panel</Link>,
                            onClick: closeMenu,
                          },
                        ]
                      : []),
                    {
                      key: "logout",
                      icon: <LogoutOutlined />,
                      label: "Sign Out",
                      onClick: () => {
                        closeMenu();
                        onSignOut?.();
                      },
                    },
                  ]
                : [
                    {
                      key: "auth-section",
                      label: "Account",
                      type: "group" as const,
                    },
                    {
                      key: "login",
                      icon: <LoginOutlined />,
                      label: <Link href="/login">Log In</Link>,
                      onClick: closeMenu,
                    },
                    {
                      key: "register",
                      label: <Link href="/register">Register</Link>,
                      onClick: closeMenu,
                    },
                  ]),
              {
                key: "theme-divider",
                type: "divider" as const,
              },
              {
                key: "theme",
                icon: <BulbOutlined />,
                label: (
                  <div className={styles.themeToggle}>
                    <span className={styles.themeLabel}>{isDark ? "Dark Theme" : "Light Theme"}</span>
                    <Switch 
                      checked={isDark} 
                      onChange={toggleTheme}
                    />
                  </div>
                ),
                onClick: () => toggleTheme(!isDark),
              },
            ]}
          />
        </div>
      )}
    </>
  );
}
