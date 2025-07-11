# Unity KnowLang

Unity plugin providing an embedded chat interface for [KnowLang](https://github.com/KnowLangOrg/know-lang) - an advanced codebase exploration tool with semantic search and intelligent Q&A capabilities.

[![KnowLang Website](https://img.shields.io/badge/🌐%20KnowLang-Website-blue)](https://github.com/KnowLangOrg/know-lang)

## Features

- 🎮 **Unity Integration**: Seamless in-editor chat interface
- 🔍 **Semantic Code Search**: Natural language queries for your codebase
- 💬 **Real-time Q&A**: Get instant answers about code functionality
- 🔌 **HTTP Communication**: Connects to KnowLang backend via HTTP

## Prerequisites

1. **Unity 6.0+**: Compatible with Unity 6.0 and newer

## Installation

### Via Package Manager (Git URL)

1. Open Unity Package Manager (`Window > Package Manager`)
2. Click `+` and select `Add package from git URL`
3. Enter: `https://github.com/knowlangorg/unityknowlang.git`

### Via Package Manager (Local)

1. Download/clone this repository
2. Open Unity Package Manager (`Window > Package Manager`)
3. Click `+` and select `Add package from disk`
4. Navigate to the package folder and select `package.json`

## Setup

### 1. Enable HTTP Connections

**Important**: Unity requires HTTP connections to be explicitly enabled.

1. Go to `Edit > Project Settings > Player`
2. Navigate to `Other Settings > Configuration`
3. Set `Allow downloads over HTTP` to `Always allowed`

![HTTP Connection Setup](HTTP_Connection.png)

### 2. Resolve Dependencies

UnityKnowLang uses Newtonsoft.Json for JSON serialization and NativeWebSocket for real-time communication. Follow these steps to ensure proper dependency resolution:

#### Option A: Via Package Manager (Recommended)

**Install Newtonsoft.Json:**

1. Open Unity Package Manager (`Window > Package Manager`)
2. Click the `+` button and select `Install package by name`
3. Enter: `com.unity.nuget.newtonsoft-json`
4. Click `Install` to add it to your project

**Install NativeWebSocket:**

1. Open Unity Package Manager (`Window > Package Manager`)
2. Click the `+` button and select `Install package from git URL`
3. Enter: `https://github.com/endel/NativeWebSocket.git#upm`
4. Click `Install` to add it to your project

#### Option B: Via manifest.json

1. Open `Packages/manifest.json` in your project
2. Add the following dependency to the `dependencies` section:
   ```json
   "com.unity.nuget.newtonsoft-json": "3.2.1",
   "com.endel.nativewebsocket": "https://github.com/endel/NativeWebSocket.git#upm"
   ```
3. Save the file and let Unity resolve the dependency

> TODO: ideally, UnityKnowLang should be properly configured to specify this dependency to prevent manual configuration.

## Usage

1. **Open Unity Plugin**: Access the chat interface through Unity's menu or inspector
2. **Parse Your Knowledge**: Parse your code, assets, visual scripts into knowlang database.
3. **Ask Questions**: Ask anythin with natural languages

## Configuration

Modify connection settings in your Unity project settings or via the plugin interface:

- **Server URL**: KnowLang backend address
- **Timeout**: Request timeout duration
- **Auto-connect**: Automatically connect on startup

## Troubleshooting

### Connection Issues

- Verify KnowLang backend is running
- Check HTTP connections are enabled in Unity
- Ensure firewall allows connections to backend

## Development

This plugin communicates with KnowLang via HTTP ASGI server. For development:

1. Clone the repository
2. Import into Unity as local package
3. Modify scripts in `Runtime/` and `Editor/` folders
4. Test with local KnowLang instance

## Support

- **Issues**: [GitHub Issues](https://github.com/knowlangorg/unityknowlang/issues)

## License

Apache License 2.0 - see [LICENSE](LICENSE.md)
