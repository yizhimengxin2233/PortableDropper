# Third-Party Notices

PortableDropper embeds or ships the following third-party components:

## 7-Zip (7z.exe / 7z.dll)

- Author: Igor Pavlov
- Website: https://www.7-zip.org/
- License: GNU Lesser General Public License (LGPL), with unRAR restriction clause
- Usage: extracted at runtime from this program's embedded resources to a
  temporary directory and run as a separate process to extract archives
  (.7z/.rar, etc.)
- Note: this component is **unmodified** and distributed as part of this program;
  per the LGPL requirement, users may replace it with another version or remove it.
- Full license text: https://www.7-zip.org/license.txt

All other code in this project (PortableDropper.cs, etc.) is licensed under the
MIT License — see the LICENSE file.