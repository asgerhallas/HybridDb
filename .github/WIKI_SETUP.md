# Wiki Sync Setup Instructions

This repository uses a hybrid documentation approach:
- Documentation source is in `/docs` folder (version controlled with code)
- Documentation is automatically synced to the GitHub Wiki for discoverability
- MarkdownSnippets keeps code examples and docs in sync

## Setup Steps

### 1. Enable Wiki for the Repository

1. Go to repository Settings
2. Scroll to "Features" section
3. Check the "Wikis" checkbox

### 2. Create a Personal Access Token (PAT)

The GitHub Actions workflow needs a PAT to write to the wiki:

1. Go to GitHub Settings → Developer settings → Personal access tokens → Tokens (classic)
2. Click "Generate new token (classic)"
3. Give it a descriptive name: `HybridDb Wiki Sync`
4. Set expiration as needed
5. Select scopes:
   - `repo` (Full control of private repositories)
6. Click "Generate token"
7. **Copy the token immediately** (you won't see it again)

### 3. Add PAT as Repository Secret

1. Go to repository Settings → Secrets and variables → Actions
2. Click "New repository secret"
3. Name: `GH_PAT`
4. Value: Paste the PAT you copied
5. Click "Add secret"

### 4. Initialize the Wiki

The wiki must exist before the sync action can run:

1. Go to the Wiki tab in your repository
2. Click "Create the first page"
3. Title: `Home`
4. Content: Any placeholder text (it will be overwritten by the sync)
5. Click "Save Page"

### 5. Trigger First Sync

Option A: Make a change to any file in `/docs` and push to master

Option B: Manually trigger the workflow:
1. Go to Actions tab
2. Select "Sync Docs to Wiki" workflow
3. Click "Run workflow"
4. Select branch (master)
5. Click "Run workflow"

## How It Works

- When you push changes to `/docs/**` on the master branch, the workflow automatically syncs to wiki
- The `Home.md` file becomes the wiki home page
- All other `.md` files in `/docs` are synced as wiki pages
- Images and assets in `/docs` are also synced

## Maintenance

- The PAT may need to be renewed based on the expiration you set
- The workflow can also be manually triggered from the Actions tab
- Wiki pages can still be edited directly in the GitHub UI, but changes will be overwritten on next sync

## Local Development

You can clone and work with the wiki locally if needed:

```bash
git clone https://github.com/asgerhallas/HybridDb.wiki.git
```

However, remember that the docs folder is the source of truth - manual wiki edits will be overwritten.
