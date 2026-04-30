import { keymap } from "@codemirror/view";
import { EditorView } from "codemirror";
import { basicSetup } from "codemirror";
import { EditorState } from "@codemirror/state";
import { javascript } from "@codemirror/lang-javascript";
import { python } from "@codemirror/lang-python";
import { html } from "@codemirror/lang-html";
import { css } from "@codemirror/lang-css";
import { json } from "@codemirror/lang-json";
import { markdown } from "@codemirror/lang-markdown";
import { oneDark } from "@codemirror/theme-one-dark";
import { HighlightStyle, syntaxHighlighting } from "@codemirror/language";
import { tags } from "@lezer/highlight";
import { autocompletion } from "@codemirror/autocomplete";
import { searchKeymap, highlightSelectionMatches } from "@codemirror/search";

let editorView: EditorView | null = null;

export function createEditor(
  parent: HTMLElement,
  initialDoc: string = "",
  language: string = "javascript",
) {
  if (editorView) {
    editorView.destroy();
    editorView = null;
  }

  let langExtension;
  switch (language) {
    case "python":
      langExtension = python();
      break;
    case "html":
      langExtension = html();
      break;
    case "css":
      langExtension = css();
      break;
    case "json":
      langExtension = json();
      break;
    case "markdown":
      langExtension = markdown();
      break;
    default:
      langExtension = javascript();
  }

  editorView = new EditorView({
    state: EditorState.create({
      doc: initialDoc,
      extensions: [
        basicSetup,
        langExtension,
        oneDark,
        autocompletion(),
        highlightSelectionMatches(),
        keymap.of(searchKeymap),
        syntaxHighlighting(
          HighlightStyle.define([
            { tag: tags.keyword, color: "#ff7b72" },
            { tag: tags.string, color: "#a5d6ff" },
            { tag: tags.comment, color: "#8b949e" },
            { tag: tags.number, color: "#d2a8ff" },
            { tag: tags.variableName, color: "#79c0ff" },
          ]),
        ),
      ],
    }),
    parent,
  });
}

export function getEditorValue(): string {
  return editorView ? editorView.state.doc.toString() : "";
}

export function setEditorValue(value: string) {
  if (editorView) {
    editorView.dispatch({
      changes: { from: 0, to: editorView.state.doc.length, insert: value },
    });
  }
}

export function destroyEditor() {
  if (editorView) {
    editorView.destroy();
    editorView = null;
  }
}
