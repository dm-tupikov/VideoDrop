# Сторонние компоненты

Лицензия MIT для кода VideoDrop не заменяет лицензии встроенных программ и их библиотек. Исполняемые файлы взяты без изменения из указанных официальных выпусков. SHA-256 закреплены в [`scripts/deps.lock.json`](scripts/deps.lock.json).

| Компонент | Версия | Лицензия / уведомление | Исходники и бинарный выпуск |
| --- | --- | --- | --- |
| yt-dlp | 2026.10.26 | [Unlicense](licenses/yt-dlp-LICENSE.txt); зависимости standalone-сборки имеют собственные лицензии | [Исходники](https://github.com/yt-dlp/yt-dlp/tree/2026.10.26), [выпуск](https://github.com/yt-dlp/yt-dlp/releases/tag/2026.10.26), [лицензирование сборок](https://github.com/yt-dlp/yt-dlp#licensing) |
| Deno | 2.9.8 | [MIT](licenses/Deno-LICENSE.txt), уведомления зависимостей в исходном проекте | [Исходники](https://github.com/denoland/deno/tree/v2.9.8), [выпуск](https://github.com/denoland/deno/releases/tag/v2.9.8) |
| FFmpeg / ffprobe, Gyan essentials | 9.0.2 | [GPLv3](licenses/LICENSE); [описание сборки](licenses/README.txt) | [FFmpeg source](https://ffmpeg.org/releases/ffmpeg-9.0.2.tar.xz), [дерево n9.0.2](https://github.com/FFmpeg/FFmpeg/tree/n9.0.2), [бинарная сборка Gyan](https://github.com/GyanD/codexffmpeg/releases/tag/9.0.2), [проект сборщика](https://github.com/GyanD/codexffmpeg) |

Внутри официального yt-dlp.exe уже включены Python, yt-dlp-ejs и другие необходимые библиотеки. Подробные сведения об их лицензиях доступны в [проекте yt-dlp](https://github.com/yt-dlp/yt-dlp#licensing) и [материалах сборки](https://github.com/yt-dlp/yt-dlp/tree/2026.10.26/bundle).

Уведомления копируются в архив инструментов и распаковываются рядом с ними в `VideoDrop-data/tools-v1/`. Кнопка обновления меняет распакованный yt-dlp на актуальный официальный выпуск; встроенная в EXE версия остаётся фиксированной.
