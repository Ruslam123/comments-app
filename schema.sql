-- Database Schema
CREATE TABLE "Users" (
    "Id" UUID PRIMARY KEY,
    "UserName" VARCHAR(100) NOT NULL,
    "Email" VARCHAR(255) NOT NULL,
    "HomePage" VARCHAR(500),
    "IpAddress" VARCHAR(50) NOT NULL,
    "UserAgent" TEXT NOT NULL,
    "CreatedAt" TIMESTAMP NOT NULL
);
CREATE INDEX "IX_Users_Email" ON "Users" ("Email");

CREATE TABLE "Comments" (
    "Id" UUID PRIMARY KEY,
    "UserId" UUID NOT NULL REFERENCES "Users"("Id"),
    "ParentCommentId" UUID REFERENCES "Comments"("Id"),
    "Text" TEXT NOT NULL,
    "ImagePath" VARCHAR(500),
    "TextFilePath" VARCHAR(500),
    "CreatedAt" TIMESTAMP NOT NULL
);
