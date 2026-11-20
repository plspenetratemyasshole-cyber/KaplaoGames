export type UserRecord = {
  id: string;
  nickname: string;
  email: string;
  passwordHash: string;
  verified: boolean;
  createdAt: string;
};

export type VerificationPurpose = "verify_email" | "reset_password";

export type VerificationCode = {
  email: string;
  code: string;
  expiresAt: string;
  purpose: VerificationPurpose;
};

export type DeviceSession = {
  code: string;
  status: "pending" | "ready" | "expired";
  token?: string;
  nickname?: string;
  email?: string;
  expiresAt: string;
};

export type AdminRecord = {
  email: string;
  createdAt: string;
  createdBy?: string;
};

export type PlayerPermission = "admin" | "level_mod" | "none";

export type PlayerRight = "level_purge" | "game_host" | "web_console";

export type PlayerRoleRecord = {
  email: string;
  /** @deprecated Derived from rights for compatibility. */
  permission: PlayerPermission;
  rights: PlayerRight[];
  nicknamePreset: string;
  updatedAt: string;
  updatedBy?: string;
};

export type NicknamePresetRecord = {
  name: string;
  createdAt: string;
  createdBy?: string;
  style: import("./nickname-preset-style").NicknamePresetStyle;
};

export type PlayerWithRole = UserRecord & {
  permission: PlayerPermission;
  rights: PlayerRight[];
  nicknamePreset: string;
};

export type DatabaseShape = {
  users: UserRecord[];
  codes: VerificationCode[];
  devices: DeviceSession[];
  admins: AdminRecord[];
  playerRoles: PlayerRoleRecord[];
  nicknamePresets: NicknamePresetRecord[];
  devMessages: DevChatMessage[];
};


export type DevChatMessage = {
  id: string;
  senderEmail: string;
  senderNickname: string;
  message: string;
  isFromAdmin: boolean;
  adminEmail?: string;
  adminNickname?: string;
  createdAt: string;
  readByAdmin: boolean;
  readByPlayer: boolean;
};

export type DevChatConversation = {
  playerEmail: string;
  playerNickname: string;
  lastMessage: string;
  lastMessageAt: string;
  unreadCount: number;
  isLastFromAdmin: boolean;
};
