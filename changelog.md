# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.0.0] - 2022-11-07

### Added
- New registration token model.
- New player pref indicating there is data from previous start. This helps the server separate new installations from regular updates.

### Changed
- Registration expanded with sending additional user info.
- Sending of most user properties moved to the server side (install dates, game versions, tester flag).

## [1.0.1] - 2022-10-20

### Added
- Changelog itself.
- Start version counter for the package.
- XML-docs for the API (that is useless with a current distribution model).

### Changed
- Time verification utility will only work for positive delta values now. Validating "outdated" timestamps is fully disabled.
