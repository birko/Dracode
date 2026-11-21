/**
 * Markdown to safe HTML for read-only screens (TASK-150). Birko's renderer (BMarkdownEditor.renderMarkdown) escapes
 * raw HTML but copies a link or image target into the attribute as written, so `javascript:` URLs and a `"` that
 * closes the attribute get through. Its output therefore goes through an allowlist: only the elements and attributes
 * the renderer emits survive, and a link or image keeps its target only when it is http(s), mailto, a fragment or
 * relative. Birko.Web has no sanitiser yet; this is the candidate to move there.
 */

import { BMarkdownEditor } from 'birko-web-components/inputs';

const ALLOWED: Record<string, readonly string[]> = {
  p: [], br: [], hr: [], blockquote: [], pre: [], code: ['class'],
  h1: [], h2: [], h3: [], h4: [], h5: [], h6: [],
  ul: [], ol: [], li: ['class'],
  strong: [], em: [], del: [], mark: [], sup: [], sub: [],
  table: [], thead: [], tbody: [], tr: [], th: [], td: [],
  a: ['href'], img: ['src', 'alt'], input: ['type', 'checked', 'disabled'],
};

/** Elements dropped with everything inside them; any other unknown element is replaced by its children. */
const DROP_WITH_CONTENT = new Set(['script', 'style', 'iframe', 'object', 'embed', 'template', 'noscript', 'svg', 'math']);

function safeUrl(value: string): boolean {
  const v = value.trim();
  if (/^(https?:|mailto:)/i.test(v)) return true;
  // No scheme at all: a fragment or a relative path. (`javascript:`, `data:`, `vbscript:` … all carry one.)
  return !/^[a-z][a-z0-9+.-]*:/i.test(v.replace(/[\u0000- ]/g, ''));
}

function clean(node: Node): void {
  for (const child of Array.from(node.childNodes)) {
    if (child.nodeType === Node.TEXT_NODE) continue;
    if (child.nodeType !== Node.ELEMENT_NODE) { child.remove(); continue; }
    const el = child as Element;
    const tag = el.tagName.toLowerCase();
    if (DROP_WITH_CONTENT.has(tag)) { el.remove(); continue; }
    const allowed = ALLOWED[tag];
    if (!allowed) {
      clean(el);
      el.replaceWith(...Array.from(el.childNodes));
      continue;
    }
    for (const attr of Array.from(el.attributes)) {
      const name = attr.name.toLowerCase();
      const keep = allowed.includes(name) && ((name !== 'href' && name !== 'src') || safeUrl(attr.value));
      if (!keep) el.removeAttribute(attr.name);
    }
    if (tag === 'input' && el.getAttribute('type') !== 'checkbox') { el.remove(); continue; }
    if (tag === 'input') el.setAttribute('disabled', '');
    if (tag === 'a' && el.hasAttribute('href') && /^https?:/i.test(el.getAttribute('href')!.trim())) {
      el.setAttribute('target', '_blank');
      el.setAttribute('rel', 'noopener noreferrer');
    }
    clean(el);
  }
}

/** Keep only the allowlisted elements and attributes of `html`. Parsing is inert: nothing in it loads or runs. */
export function sanitizeHtml(html: string): string {
  const doc = new DOMParser().parseFromString(`<body>${html}</body>`, 'text/html');
  clean(doc.body);
  return doc.body.innerHTML;
}

/**
 * Undo two quirks of Birko's renderer: a list that follows a paragraph line with no blank line between gets a `<br>`
 * between its items, and a fenced block keeps the newline after its opening fence.
 */
function tidy(html: string): string {
  const doc = new DOMParser().parseFromString(`<body>${html}</body>`, 'text/html');
  doc.body.querySelectorAll('ul > br, ol > br').forEach((br) => br.remove());
  doc.body.querySelectorAll('pre > code').forEach((code) => {
    const first = code.firstChild;
    if (first?.nodeType === Node.TEXT_NODE && first.textContent?.startsWith('\n')) {
      first.textContent = first.textContent.slice(1);
    }
  });
  return doc.body.innerHTML;
}

/** Render Markdown for display. */
export function renderMarkdown(source: string): string {
  return tidy(sanitizeHtml(BMarkdownEditor.renderMarkdown(source)));
}
