# The AFTERLIFE guide website

A small static site made from the Markdown files of this repository, so the guide on the web is always the guide in the
repository: `docs/USER-GUIDE.md`, `docs/FAQ.md`, `PRIVACY.md` and `CHANGELOG.md`. The start page is `home.md` in this folder.

```
cd site
npm ci
npm run build
```

The result is `site/dist` (not committed). Open `dist/index.html` in a browser to look at it.

## Cloudflare Pages

Connect the repository and set:

| Setting | Value |
|---|---|
| Production branch | `main` |
| Root directory | `site` |
| Build command | `npm ci && npm run build` |
| Build output directory | `dist` |

Every push to `main` then publishes the site again. To change a text, edit the Markdown file; to change the look, edit
`style.css` (colours and fonts are the tokens of the app in `src/Afterlife.App/App.xaml`).
