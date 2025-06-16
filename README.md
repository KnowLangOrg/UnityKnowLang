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

### Performance
- Consider running KnowLang backend on local network for faster responses
- Adjust timeout settings based on query complexity

## Development

This plugin communicates with KnowLang via HTTP ASGI server. For development:

1. Clone the repository
2. Import into Unity as local package
3. Modify scripts in `Runtime/` and `Editor/` folders
4. Test with local KnowLang instance

## Support

- **Issues**: [GitHub Issues](https://github.com/knowlangorg/unityknowlang/issues)
- **KnowLang Docs**: [knowlang.dev](https://github.com/knowlangorg/know-lang)
- **Unity Forum**: [Unity Package Support](link-to-unity-forum)

## License

Apache License 2.0 - see [LICENSE](LICENSE.md)