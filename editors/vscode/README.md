# ACSDlg — VS Code extension

Syntax highlighting + snippets for the ACSDlg dialogue DSL (`.acsdlg`).

## Install (no build needed)

Copy this folder into your VS Code extensions directory, then reload VS Code:

- Windows: `%USERPROFILE%\.vscode\extensions\acsdlg-0.1.0\`
- macOS / Linux: `~/.vscode/extensions/acsdlg-0.1.0/`

```
cp -r editors/vscode "$HOME/.vscode/extensions/acsdlg-0.1.0"
```

Open any `.acsdlg` file → highlighting is active. Type `node`, `line`, `choicejump`, `choiceblock`, `start` for snippets.

## Develop

Open this folder in VS Code and press `F5` to launch an Extension Development Host.

## Highlights

- `#start`, `node`, `jump`, `choice`, `end`
- node / jump targets, choice labels (strings)
- speaker name before `:`
- inline markup `[speed:..] [s:..] [pause:..] [p:..] [tp] [emit:..]`
- `//` line comments

## Not included (yet)

Live error checking (missing `;`, jump to unknown node…) would need a language server
that calls the C# parser. Bigger task — deferred.
