# Third-party notices

kyklos is licensed under the GNU General Public License v3.0 (see LICENSE). It's built on, and its release zips carry,
the components below, each under its own licence.

| Component | What kyklos uses it for | Licence |
|---|---|---|
| [.NET](https://github.com/dotnet/runtime) | the runtime kyklos runs on | MIT, © .NET Foundation and Contributors; its own third-party notices are in `licenses/.NET third-party notices.txt` |
| [Windows App SDK](https://github.com/microsoft/WindowsAppSDK) (WinUI 3) | windows, controls, notifications | Microsoft Software License Terms, in `licenses/Windows App SDK license.txt` |
| Windows SDK for .NET (Microsoft.Windows.SDK.NET, WinRT.Runtime) | calling Windows from C# | Microsoft Windows SDK license terms: https://aka.ms/WinSDKLicenseURL |
| [Windows Community Toolkit](https://github.com/CommunityToolkit/Windows) | the phase switcher and the settings cards | MIT, © .NET Foundation and Contributors |
| [Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons) | the Focus, Short break and Long break icons (brain_circuit, drink_coffee and bed, regular 20) | MIT, © 2020 Microsoft Corporation |

[C#/Win32](https://github.com/microsoft/CsWin32) (MIT, © Microsoft Corporation) generates kyklos's calls to Windows
while it's built; none of it ships.

The MIT License, as it applies to the components above marked MIT, with their copyright lines:

```
MIT License

Copyright (c) <the copyright line above>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
