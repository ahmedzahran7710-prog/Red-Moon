# Third-party notices

## Map subsystem

The map subsystem files under `src/Canyon.World/Map/` are adapted from the MIT-licensed LongV6270 project:

- Repository: https://github.com/darkfoxdeveloper/LongV6270
- Source commit: `70d3a8099323c697177cdcdd5ac1f00ca800796f`
- Original paths: `src/Long.World/Map/`
- Copyright (c) 2024 Cristian Ocaña Soler
- License: MIT

Adaptations in this repository include the `Canyon.*` namespaces and replacement of Serilog calls with the server's existing `LogFactory` / `Microsoft.Extensions.Logging` APIs. The target projects remain on .NET 7 and their existing package versions.

MIT License

Copyright (c) 2024 Cristian Ocaña Soler

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
