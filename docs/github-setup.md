# GitHub Setup

The ChatGPT GitHub connector used during project planning may not expose repository creation.

## Option A: GitHub CLI

```powershell
cd OpenMagicKeyboardWin
gh auth login
gh repo create hhkb-ai/OpenMagicKeyboardWin --public --source . --remote origin --push
```

## Option B: GitHub website

1. Open GitHub.
2. Create a new public repository named `OpenMagicKeyboardWin` under `hhkb-ai`.
3. Do not initialize with README, license, or .gitignore.
4. Run:

```powershell
cd OpenMagicKeyboardWin
git init
git add .
git commit -m "Initial clean-room project scaffold"
git branch -M main
git remote add origin https://github.com/hhkb-ai/OpenMagicKeyboardWin.git
git push -u origin main
```
