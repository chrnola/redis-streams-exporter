# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## Released

## 0.6 - 2021-12-08
### Added
- Multi-key support. `REDIS_STREAM_KEY` can now contain a semi-colon delimited list of keys to be monitored concurrently.

## 0.5 - 2020-09-10
### Fixed
- No longer crashes when monitoring a consumer that has been idle for > 24 days

## 0.4 - 2020-08-12
### Fixed
- No longer crashes when the stream is not fully initialized

## 0.3 - 2020-08-12
### Added
- Added Serilog for structured logging
- `APP_ENABLE_VERBOSE_LOGGING` environment variable to control log verbosity

### Fixed
- App no longer crashes when the key to monitor doesn't exist.

## 0.2 - 2020-08-11
### Changed
- Base Docker image to `dotnet/core/runtime:3.1-alpine` to drastically reduce size of final image.

## 0.1 - 2020-08-10
- Initial release
