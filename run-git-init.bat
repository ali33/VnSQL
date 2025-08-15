@echo off
set /p "origin=Enter Git Url: "
git init
git add .
git commit -m "first commit"
git branch -M main
git remote add origin %origin%
git push -u origin main