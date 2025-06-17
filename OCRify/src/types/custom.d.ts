declare module 'marked' {
  export function marked(text: string): string;
  export default marked;
}

declare module 'katex' {
  export function renderToString(tex: string, options?: any): string;
  export default {
    renderToString
  };
} 