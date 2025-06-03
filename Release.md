# Creating a Release

This project uses GitHub Actions to automate the build and release process. New releases are created when a new version tag (e.g., `v1.0.0`, `v1.0.1`) is pushed to the repository.

To create a new release:

1.  Ensure all your changes for the release are committed to the main branch.
2.  Optionally, update the version number in `DatabaseDock.csproj` if you manage it manually.
3.  Create a new Git tag for the version. For example, for version 1.0.0:
    ```bash
    git tag v1.0.0
    ```
4.  Push the tag to the GitHub repository:
    ```bash
    git push origin v1.0.0
    ```

Pushing the tag will trigger the GitHub Action workflow defined in `.github/workflows/build-release.yml`. This workflow will:
- Build the application.
- Publish a self-contained `.exe` file.
- Create a new GitHub Release with the tag name.
- Upload the `.exe` file as an asset to the release, making it available for download.

You can monitor the progress of the action in the "Actions" tab of the GitHub repository.
