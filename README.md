# DatabaseDock

DatabaseDock is a Windows desktop application for managing Docker-based database containers. It provides a user-friendly interface to create, configure, start, and stop database containers for development and testing purposes.

## Features

- Manage MySQL, MSSQL, PostgreSQL, and Redis containers via Docker
- Configure volume paths for database data persistence
- Start/stop database containers with a single click
- System tray integration (minimize to tray, start with Windows)
- Easy access to database connection information
- Modern and intuitive user interface

## Requirements

- Windows 10/11
- .NET 9.0 or higher
- Docker Desktop for Windows

## Getting Started

1. Make sure Docker Desktop is running on your system
2. Launch DatabaseDock
3. Configure volume paths for your databases in the settings
4. Start the database containers you need
5. Use the connection information to connect to your databases

## Development

This project is built with:
- C# and WPF for the UI
- Docker.DotNet for Docker integration
- Hardcodet.NotifyIcon.Wpf for system tray functionality

## License

MIT License
