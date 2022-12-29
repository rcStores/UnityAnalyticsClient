# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.0.7] - 2022-12-29

### Added
- Native .NET http client


## [2.0.6] - 2022-12-22

### Fixed
- JSON session building.
- String values escaping.

## [2.0.5] - 2022-12-17

### Fixed
- NullReferenceException while handling http response.


## [2.0.4] - 2022-12-12

### Fixed
- Crashes occuring due to invoking DTDLogger's methods before the object is initialized.

## [2.0.3] - 2022-12-12

### Added
- Some more logging.


## [2.0.2] - 2022-12-12

### Added
- DevToDev logger receiving delegates for sending analytic data within the SDK.
- Static wrapper method for logging without deep injection of the logger class.
- Wrapper methods' invocations.

## [2.0.1] - 2022-11-09

### Changed
- Bugfix: remove an attempt to access non-existing game session.


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
