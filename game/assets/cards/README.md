# Карты (арт)

Нарезанные сканы из `Карты и ширмы/карты 01–09.jpg` по полю `source` в `data/cards.json`.

| Файл | Содержимое |
|---|---|
| `001.png`…`088.png` | Лицевая сторона карты по id |
| `back.png` | Рубашка |

Пересобрать:

```bash
python3 tools/slice_cards.py
```

Требуется Pillow (`pip install pillow`).
