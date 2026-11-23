// Builds the guide website into site/dist from the Markdown files of the repository.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { marked } from 'marked';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..');
const out = path.join(here, 'dist');
const source = 'https://github.com/silentbobcz/afterlife';

const pages = [
  { file: 'site/home.md', to: 'index.html', nav: 'Home', toc: false },
  { file: 'docs/USER-GUIDE.md', to: 'guide.html', nav: 'Guide', toc: true },
  { file: 'docs/FAQ.md', to: 'faq.html', nav: 'FAQ', toc: true },
  { file: 'CHANGELOG.md', to: 'changelog.html', nav: 'Changelog', toc: false },
  { file: 'PRIVACY.md', to: 'privacy.html', nav: 'Privacy', toc: false },
];

// Markdown files that are pages here keep working as links; every other relative link goes to the repository.
const pageOf = new Map(pages.map(p => [path.posix.normalize(p.file), p.to]));

const escapeHtml = s => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
const plain = html => html.replace(/<[^>]+>/g, '').replace(/&amp;/g, '&').replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&#39;/g, "'");

/** The anchor GitHub gives a heading, so links written for GitHub work here too. */
function slug(text, used) {
  const base = text.trim().toLowerCase().replace(/[^\p{L}\p{N}\s_-]/gu, '').replace(/\s/g, '-');
  const n = used.get(base) ?? 0;
  used.set(base, n + 1);
  return n === 0 ? base : `${base}-${n}`;
}

function rewriteLink(href, fromFile) {
  if (/^(https?:|mailto:|#)/.test(href)) return href;
  const [target, hash = ''] = href.split('#');
  const resolved = path.posix.normalize(path.posix.join(path.posix.dirname(fromFile), target));
  const page = pageOf.get(resolved);
  if (page) return page + (hash ? '#' + hash : '');
  return `${source}/blob/main/${resolved}${hash ? '#' + hash : ''}`;
}

function render(page) {
  let md = fs.readFileSync(path.join(repo, page.file), 'utf8').replace(/\r\n/g, '\n');
  let title = 'AFTERLIFE';
  md = md.replace(/^# (.+)\n/, (_, t) => { title = t.trim(); return ''; });
  // the list of sections at the top of a document is replaced by the side menu
  if (page.toc) md = md.replace(/^(?:- \[[^\n]+\n)+\n(?:---\n)?/m, '');

  let html = marked.parse(md, { gfm: true });
  const used = new Map();
  const sections = [];
  html = html.replace(/<h([23])>([\s\S]*?)<\/h\1>/g, (_, level, inner) => {
    const text = plain(inner);
    const id = slug(text, used);
    if (level === '2') sections.push({ id, text });
    return `<h${level} id="${id}"><a class="anchor" href="#${id}">${inner}</a></h${level}>`;
  });
  html = html.replace(/href="([^"]+)"/g, (_, href) => `href="${escapeHtml(rewriteLink(href.replace(/&amp;/g, '&'), page.file))}"`);
  html = html.replace(/<table>/g, '<div class="table"><table>').replace(/<\/table>/g, '</table></div>');
  return { title, html, sections };
}

function shell(page, { title, html, sections }) {
  const nav = pages.map(p => `<a href="${p.to}"${p === page ? ' class="on"' : ''}>${p.nav}</a>`).join('');
  const side = page.toc && sections.length
    ? `<nav class="side" aria-label="Sections"><div class="side-title">// ${escapeHtml(page.nav.toUpperCase())}</div>${sections.map(s => `<a href="#${s.id}">${escapeHtml(s.text)}</a>`).join('')}</nav>`
    : '';
  const home = page.to === 'index.html';
  const heading = home
    ? `<header class="hero"><img class="hero-logo" src="assets/logo.png" alt="" width="112" height="112"><div><div class="wordmark" data-text="AFTERLIFE">AFTERLIFE</div><div class="kicker">MOD MANAGER FOR CYBERPUNK 2077 <span class="cursor"></span></div><h1>${escapeHtml(title)}</h1></div></header>`
    : `<div class="kicker page-kicker">// AFTERLIFE · ${escapeHtml(page.nav.toUpperCase())}</div><h1>${escapeHtml(title)}</h1>`;
  return `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>${escapeHtml(page.to === 'index.html' ? 'AFTERLIFE · mod manager for Cyberpunk 2077' : `${title} · AFTERLIFE`)}</title>
<meta name="description" content="AFTERLIFE is a safe, open-source mod manager for Cyberpunk 2077 on Windows: user guide, FAQ, changelog and privacy.">
<link rel="icon" href="assets/logo.png">
<link rel="stylesheet" href="style.css">
</head>
<body>
<div class="top"><a class="brand" href="index.html"><img src="assets/logo.png" alt="" width="28" height="28">AFTERLIFE</a><nav class="main">${nav}<a href="${source}">GitHub</a></nav></div>
<div class="wrap${side ? ' with-side' : ''}${home ? ' home' : ''}">
${side}
<main>
${heading}
${html}
</main>
</div>
<footer><div class="foot-brand">AFTERLIFE <span>// free &amp; open source</span></div>© 2026 Silentbobcz · MIT License · AFTERLIFE is an unofficial fan work and is not approved or endorsed by CD PROJEKT RED. Cyberpunk 2077 is a trademark of CD PROJEKT S.A. Nexus Mods is a trademark of Black Tree Gaming Ltd.</footer>
</body>
</html>
`;
}

fs.rmSync(out, { recursive: true, force: true });
fs.mkdirSync(path.join(out, 'assets'), { recursive: true });
for (const page of pages) fs.writeFileSync(path.join(out, page.to), shell(page, render(page)));
fs.copyFileSync(path.join(here, 'style.css'), path.join(out, 'style.css'));
fs.cpSync(path.join(here, 'screenshots'), path.join(out, 'screenshots'), { recursive: true });
fs.copyFileSync(path.join(repo, 'branding/out/afterlife-logo-transparent-512.png'), path.join(out, 'assets/logo.png'));
const fonts = path.join(repo, 'src/Afterlife.App/Assets/Fonts');
for (const f of ['ChakraPetch-Regular.ttf', 'ChakraPetch-SemiBold.ttf', 'ChakraPetch-Bold.ttf', 'JetBrainsMono.ttf', 'OFL-ChakraPetch.txt', 'OFL-JetBrainsMono.txt'])
  fs.copyFileSync(path.join(fonts, f), path.join(out, 'assets', f));
console.log(`${pages.length} pages in ${path.relative(repo, out)}`);
