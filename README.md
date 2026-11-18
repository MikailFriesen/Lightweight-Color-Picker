# Color Picker

A tiny portable Windows color picker (a single ~13 KB `.exe`, no install).

## Features
- **Ctrl+Shift+P** starts picking (works while the app is open, even if it isn't focused)
- Click anywhere on screen to pick the color under the cursor (the click is blocked, so nothing underneath gets clicked). Right-click or Esc cancels.
- Live preview of the color under the cursor
- Copy exactly the part you need: `R`, `G`, `B`, `R, G, B`, `rgb(R, G, B)`, `#RRGGBB` or `RRGGBB`
- Choose what gets auto-copied right after a pick
- History of picked colors (click one to load it again)
- Correct on high-DPI / scaled displays

## Download
Download [`ColorPicker.exe`](ColorPicker.exe?raw=true) from this repo and double-click it. Needs .NET Framework 4, which ships with Windows 10 and 11.

## Build it yourself
Uses the C# compiler that comes with Windows:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize /out:ColorPicker.exe ColorPicker.cs
```
