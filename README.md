# 🎮 POPSManager.DBGenerator

**Generador automático de base de datos de metadatos para juegos de PS1 y PS2**, diseñado para alimentar aplicaciones cliente (Android, Windows) compatibles con **OPL** (Open PS2 Loader).

[![Generate DB](https://github.com/Kiba1585/POPSManager.DBGenerator/actions/workflows/generate-db.yml/badge.svg)](https://github.com/Kiba1585/POPSManager.DBGenerator/actions/workflows/generate-db.yml)

---

## 📦 ¿Qué hace?

Cada mes (o manualmente) construye una base de datos unificada con:

- ✅ **Todos los juegos de PS1 y PS2** listados en Redump (+20.000 títulos)
- ✅ **Nombres traducidos automáticamente al español** (inglés → español + otros idiomas)
- ✅ **Metadatos avanzados**: género, jugadores, desarrollador, fecha de lanzamiento, descripción
- ✅ **URLs de carátulas** (priorizando GameDB-PSX, con fallback al mirror de OPL Manager)
- ✅ **Detección de discos múltiples** (por título y por seriales consecutivos)
- ✅ **Archivos `.cfg` listos para OPL** (compatibilidad, modos de vídeo, etc.)
- ✅ **Distribución en dos formatos**: ZIP completo y ZIP con archivos individuales

---

## 🧱 Arquitectura

El proyecto se ha refactorizado siguiendo una **arquitectura limpia por capas** para facilitar el mantenimiento, las pruebas y la extensibilidad.

```

POPSManager.DBGenerator/
├── Core/
│   ├── Models/                  # Entidades del dominio (GameEntry)
│   └── Interfaces/              # Contratos (IGameSource, ICoverProvider, ITranslator)
├── Infrastructure/
│   ├── Parsers/                 # Lectura de datfiles Redump (RedumpParser, CfgParser)
│   ├── Providers/               # Proveedores de carátulas (OplCoverProvider, ExtraUrlsCoverProvider, FallbackCoverProvider)
│   └── Translators/             # Traducción automática (MyMemoryTranslator)
├── Application/
│   ├── Pipeline/                # Orquestación del flujo de datos (DatabaseGenerationPipeline)
│   └── Builders/                # Construcción de salidas (OutputBuilder)
├── CLI/
│   └── Program.cs               # Punto de entrada minimalista
├── Data/                        # Archivos de entrada (datfiles, cachés, CFGs)
├── Output/                      # Resultados generados (ZIPs)
└── .github/workflows/           # Automatización CI/CD

```

### 🧩 Pipeline de Procesamiento

1. **Cargar fuentes** → Redump, CFG Database, GameDB-PSX
2. **Normalizar IDs** → mayúsculas, reemplazar `-` por `_`
3. **Fusionar fuentes** → unificar por Game ID único
4. **Enriquecer con datos CFG** → género, jugadores, desarrollador, fecha, descripción
5. **Enriquecer con traducciones** → nombres y descripciones multi-idioma
6. **Enriquecer con carátulas** → URLs de GameDB-PSX o fallback a OPL Manager
7. **Post-procesar** → detección de discos múltiples
8. **Generar salidas** → JSON completos, archivos individuales, índice
9. **Empaquetar** → ZIP completo + ZIP individual

---

## 📊 Formatos de Salida

### ZIP Completo (`POPSManager_DB.zip`)
```

ps1db.json          # Todos los juegos de PS1
ps2db.json          # Todos los juegos de PS2
CFG/                # Archivos .cfg para OPL
SLUS_01234.cfg
...

```

### ZIP Individual (`POPSManager_DB_individual.zip`)
```

index.json          # Índice general (Game ID → ruta relativa)
ps1/                # JSONs individuales de PS1
SLUS_01234.json
...
ps2/                # JSONs individuales de PS2
cfg/                # Archivos .cfg individuales

```

### 📄 Estructura de un JSON de juego

```json
{
  "SLUS_01234": {
    "name": "Gran Turismo",
    "names": {
      "en": "Gran Turismo",
      "es": "Gran Turismo",
      "fr": "Gran Turismo"
    },
    "discNumber": 1,
    "coverUrl": "https://archive.org/download/oplm-art-2023-11/ART/SLUS_01234.jpg",
    "genre": "Racing",
    "players": "1-2",
    "developer": "Polyphony Digital",
    "releaseDate": "1997-12-23",
    "description": {
      "en": "The real driving simulator.",
      "es": "El verdadero simulador de conducción."
    }
  }
}
```

---

🚀 Uso

🖥️ Localmente

```bash
# Requisitos: .NET 8 SDK
git clone https://github.com/Kiba1585/POPSManager.DBGenerator.git
cd POPSManager.DBGenerator

# Coloca los datfiles en Data/psx.dat y Data/ps2.dat
dotnet run --project POPSManager.DBGenerator
```

☁️ GitHub Actions (automático)

El workflow se ejecuta automáticamente el día 1 de cada mes. También puedes lanzarlo manualmente desde la pestaña Actions.

Los archivos generados se publican como una Release de GitHub, accesibles desde:

```
https://github.com/Kiba1585/POPSManager.DBGenerator/releases/latest
```

---

📥 Consumo desde Apps Cliente

Descargar la última versión completa

```
https://github.com/Kiba1585/POPSManager.DBGenerator/releases/latest/download/POPSManager_DB.zip
```

Descargar la última versión individual

```
https://github.com/Kiba1585/POPSManager.DBGenerator/releases/latest/download/POPSManager_DB_individual.zip
```

Verificar nuevas versiones (API de GitHub)

```bash
curl https://api.github.com/repos/Kiba1585/POPSManager.DBGenerator/releases/latest
```

Leer tag_name y comparar con el valor almacenado localmente.

---

🌐 Idiomas Soportados

Idioma Código Campo en JSON
Inglés en names.en, description.en
Español es names.es, description.es
Francés fr names.fr, description.fr
Alemán de names.de, description.de
Italiano it names.it, description.it
Japonés ja names.ja

La traducción se realiza mediante la API gratuita de MyMemory y se cachea para ejecuciones futuras.

---

🛠️ Tecnologías

· .NET 8
· C# (top-level statements)
· System.Text.Json
· System.IO.Compression
· MyMemory API (traducción)
· GitHub Actions
· Python 3 (procesamiento auxiliar en el workflow)

---

🤝 Contribuir

Si quieres añadir nuevas fuentes de datos, idiomas o mejorar la detección de metadatos:

1. Implementa las interfaces IGameSource, ICoverProvider o ITranslator.
2. Registra tu implementación en Program.cs.
3. Abre un Pull Request.

---

📝 Licencia

Este proyecto se distribuye bajo la licencia MIT. Ver archivo LICENSE para más detalles.

---

👤 Autor

Kiba1585 — GitHub

---

Generado con ❤️ para la comunidad de jugadores de PS1 y PS2.

```