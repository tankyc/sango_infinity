-- CreateTable
CREATE TABLE "User" (
    "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "username" TEXT NOT NULL,
    "password" TEXT NOT NULL,
    "nickname" TEXT,
    "avatar" TEXT,
    "role" TEXT NOT NULL DEFAULT 'user',
    "createdAt" DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- CreateTable
CREATE TABLE "Mod" (
    "id" TEXT NOT NULL PRIMARY KEY,
    "authorId" INTEGER NOT NULL,
    "name" TEXT NOT NULL,
    "summary" TEXT NOT NULL DEFAULT '',
    "description" TEXT NOT NULL DEFAULT '',
    "category" TEXT NOT NULL DEFAULT 'mixed',
    "posterUrl" TEXT NOT NULL DEFAULT '',
    "gameVersion" TEXT NOT NULL DEFAULT '*',
    "status" TEXT NOT NULL DEFAULT 'pending',
    "subscribers" INTEGER NOT NULL DEFAULT 0,
    "likes" INTEGER NOT NULL DEFAULT 0,
    "views" INTEGER NOT NULL DEFAULT 0,
    "downloads" INTEGER NOT NULL DEFAULT 0,
    "createdAt" DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" DATETIME NOT NULL,
    CONSTRAINT "Mod_authorId_fkey" FOREIGN KEY ("authorId") REFERENCES "User" ("id") ON DELETE RESTRICT ON UPDATE CASCADE
);

-- CreateTable
CREATE TABLE "ModVersion" (
    "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "modId" TEXT NOT NULL,
    "version" TEXT NOT NULL,
    "changelog" TEXT NOT NULL DEFAULT '',
    "size" INTEGER NOT NULL,
    "fileKey" TEXT NOT NULL,
    "fileHash" TEXT NOT NULL,
    "depends" TEXT NOT NULL DEFAULT '',
    "gameVersion" TEXT NOT NULL DEFAULT '*',
    "downloads" INTEGER NOT NULL DEFAULT 0,
    "createdAt" DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "ModVersion_modId_fkey" FOREIGN KEY ("modId") REFERENCES "Mod" ("id") ON DELETE CASCADE ON UPDATE CASCADE
);

-- CreateTable
CREATE TABLE "ModTag" (
    "modId" TEXT NOT NULL,
    "tag" TEXT NOT NULL,

    PRIMARY KEY ("modId", "tag"),
    CONSTRAINT "ModTag_modId_fkey" FOREIGN KEY ("modId") REFERENCES "Mod" ("id") ON DELETE CASCADE ON UPDATE CASCADE
);

-- CreateTable
CREATE TABLE "ModContent" (
    "modId" TEXT NOT NULL PRIMARY KEY,
    "scenarios" TEXT NOT NULL DEFAULT '[]',
    "persons" TEXT NOT NULL DEFAULT '[]',
    "skills" TEXT NOT NULL DEFAULT '[]',
    "buffs" TEXT NOT NULL DEFAULT '[]',
    "files" TEXT NOT NULL DEFAULT '[]',
    "override" TEXT NOT NULL DEFAULT '[]',
    CONSTRAINT "ModContent_modId_fkey" FOREIGN KEY ("modId") REFERENCES "Mod" ("id") ON DELETE CASCADE ON UPDATE CASCADE
);

-- CreateTable
CREATE TABLE "Subscription" (
    "userId" INTEGER NOT NULL,
    "modId" TEXT NOT NULL,
    "localVersion" TEXT,
    "enabled" BOOLEAN NOT NULL DEFAULT false,
    "syncedAt" DATETIME NOT NULL,

    PRIMARY KEY ("userId", "modId"),
    CONSTRAINT "Subscription_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User" ("id") ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT "Subscription_modId_fkey" FOREIGN KEY ("modId") REFERENCES "Mod" ("id") ON DELETE CASCADE ON UPDATE CASCADE
);

-- CreateTable
CREATE TABLE "Comment" (
    "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "modId" TEXT NOT NULL,
    "userId" INTEGER NOT NULL,
    "parentId" INTEGER,
    "content" TEXT NOT NULL,
    "likes" INTEGER NOT NULL DEFAULT 0,
    "createdAt" DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "Comment_modId_fkey" FOREIGN KEY ("modId") REFERENCES "Mod" ("id") ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT "Comment_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User" ("id") ON DELETE CASCADE ON UPDATE CASCADE
);

-- CreateTable
CREATE TABLE "Like" (
    "userId" INTEGER NOT NULL,
    "modId" TEXT NOT NULL,
    "createdAt" DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY ("userId", "modId"),
    CONSTRAINT "Like_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User" ("id") ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT "Like_modId_fkey" FOREIGN KEY ("modId") REFERENCES "Mod" ("id") ON DELETE CASCADE ON UPDATE CASCADE
);

-- CreateTable
CREATE TABLE "DownloadLog" (
    "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "modId" TEXT NOT NULL,
    "version" TEXT NOT NULL,
    "userId" INTEGER,
    "ipHash" TEXT NOT NULL,
    "userAgent" TEXT NOT NULL DEFAULT '',
    "createdAt" DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "DownloadLog_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User" ("id") ON DELETE SET NULL ON UPDATE CASCADE
);

-- CreateIndex
CREATE UNIQUE INDEX "User_username_key" ON "User"("username");

-- CreateIndex
CREATE INDEX "Mod_status_category_idx" ON "Mod"("status", "category");

-- CreateIndex
CREATE INDEX "Mod_authorId_idx" ON "Mod"("authorId");

-- CreateIndex
CREATE INDEX "Mod_createdAt_idx" ON "Mod"("createdAt");

-- CreateIndex
CREATE INDEX "Mod_updatedAt_idx" ON "Mod"("updatedAt");

-- CreateIndex
CREATE INDEX "ModVersion_modId_createdAt_idx" ON "ModVersion"("modId", "createdAt");

-- CreateIndex
CREATE UNIQUE INDEX "ModVersion_modId_version_key" ON "ModVersion"("modId", "version");

-- CreateIndex
CREATE INDEX "ModTag_tag_idx" ON "ModTag"("tag");

-- CreateIndex
CREATE INDEX "Subscription_modId_idx" ON "Subscription"("modId");

-- CreateIndex
CREATE INDEX "Comment_modId_createdAt_idx" ON "Comment"("modId", "createdAt");

-- CreateIndex
CREATE INDEX "DownloadLog_modId_createdAt_idx" ON "DownloadLog"("modId", "createdAt");

-- CreateIndex
CREATE INDEX "DownloadLog_ipHash_modId_version_createdAt_idx" ON "DownloadLog"("ipHash", "modId", "version", "createdAt");
