# Changelog

## [1.7.0](https://github.com/ClementBobin/BOB1-Api/compare/v1.6.0...v1.7.0) (2026-11-13)

### ✨ Features

* **notifications:** reminders, ride/slot/venue alerts, deactivation and season-end notices ([bd06a40](https://github.com/ClementBobin/BOB1-Api/commit/bd06a40a0dceb9cc08fc06f5cb80d742f0a6e5d1))

## [1.6.0](https://github.com/ClementBobin/BOB1-Api/compare/v1.5.0...v1.6.0) (2026-11-13)

### ✨ Features

* Add migration files for MatchNoDivision across multiple modules ([97c57c4](https://github.com/ClementBobin/BOB1-Api/commit/97c57c4f1320bd4dccb2924b0e43119bd5b7beba))

## [1.5.0](https://github.com/ClementBobin/BOB1-Api/compare/v1.4.0...v1.5.0) (2026-11-13)

### ✨ Features

* Enhance team and family notification handling by updating match and identity APIs ([8ff8dcd](https://github.com/ClementBobin/BOB1-Api/commit/8ff8dcd833fd5a49126e742afba16acd6941db4c))
* Refactor match handling by removing DivisionId from Match and related DTOs ([425067c](https://github.com/ClementBobin/BOB1-Api/commit/425067c37c264bdb1c5cce7255ac7364ea894624))
* Remove DivisionId from Match-related classes and update mappings accordingly ([c0d9702](https://github.com/ClementBobin/BOB1-Api/commit/c0d97021764568aa7a24eefc7c1f24aec6b0ba60))

### 📝 Documentation

* add additional notification candidates and consolidated trigger table ([dce6a61](https://github.com/ClementBobin/BOB1-Api/commit/dce6a61963dc80190c5e66130cf88e8a7908db3d))
* restrict email to account-security notifications, note team memberships ([fe1ceeb](https://github.com/ClementBobin/BOB1-Api/commit/fe1ceeb318d04950e5f6fb4656e76d6dd32d14a9))

## [1.4.0](https://github.com/ClementBobin/BOB1-Api/compare/v1.3.0...v1.4.0) (2026-11-12)

### ✨ Features

* enhance CORS configuration with preflight max age ([f113b2b](https://github.com/ClementBobin/BOB1-Api/commit/f113b2b6b7ddf0536c6df2fdd3ffadb43c7601ae))

## [1.3.0](https://github.com/ClementBobin/BOB1-Api/compare/v1.2.1...v1.3.0) (2026-11-12)

### ✨ Features

* update CORS configuration to allow all origins temporarily ([5cf163a](https://github.com/ClementBobin/BOB1-Api/commit/5cf163ac335af0fcdc5a30f2697e1dde26fecc2b))

## [1.2.1](https://github.com/ClementBobin/BOB1-Api/compare/v1.2.0...v1.2.1) (2026-11-12)

### ♻️ Refactoring

* comment out CORS configuration in Program.cs for future adjustments ([619525c](https://github.com/ClementBobin/BOB1-Api/commit/619525cdd60025a7a8b0def34d165845bb2fc05e))

## [1.2.0](https://github.com/ClementBobin/BOB1-Api/compare/v1.1.0...v1.2.0) (2026-11-12)

### ✨ Features

* update CORS configuration to support dynamic allowed origins ([d3f44ae](https://github.com/ClementBobin/BOB1-Api/commit/d3f44aeea0f12bda570219f6e207fdd48ddff09f))

## [1.1.0](https://github.com/ClementBobin/BOB1-Api/compare/v1.0.0...v1.1.0) (2026-11-12)

### ✨ Features

* Add comprehensive notification trigger architecture and detailed paths ([1c7ef21](https://github.com/ClementBobin/BOB1-Api/commit/1c7ef21a2056a64ba3781580fa277af0784c4a4e))
* Add initial migrations for Identity, Matches, Notifications, Participation, and Ranking modules ([d3cd855](https://github.com/ClementBobin/BOB1-Api/commit/d3cd8554b315d32e40edf10c5ce9a1d1f97cdd7e))
* Add notification handling tasks and descriptions to TODO.md ([40d39d2](https://github.com/ClementBobin/BOB1-Api/commit/40d39d2c6271bcd7a10c35d4f05496b6b025ee4c))
* Update .gitignore to include .env and secrets/firebase.json ([e995419](https://github.com/ClementBobin/BOB1-Api/commit/e99541938d6d004258442ea53db7bf5ec73d070c))

## 1.0.0 (2026-11-12)

### ✨ Features

* add .env.example and docker-compose.yml for environment configuration ([4e99708](https://github.com/ClementBobin/BOB1-Api/commit/4e99708d85e05165b3f28ddb722e2810eeb6b9d3))
* add ASPNETCORE_ENVIRONMENT variable to Dockerfile for production configuration ([babaf37](https://github.com/ClementBobin/BOB1-Api/commit/babaf37bcbb89ac34072b8c29f4daf7f6bf25d07))
* add Dockerfile and .dockerignore for containerization setup ([ec0fcb4](https://github.com/ClementBobin/BOB1-Api/commit/ec0fcb4d4d7f62838b0c174247e27be5f3d192b4))
* add initial migration with complete schema for database entities ([049a2af](https://github.com/ClementBobin/BOB1-Api/commit/049a2afe143a53f3e50b02fa8a4def6b5a7ba470))
* add initial migration with complete schema for database entities ([22d00e4](https://github.com/ClementBobin/BOB1-Api/commit/22d00e4ec0df0d15aa24beae700c73d22808f973))
* add Ranking module with database context and initial setup ([632ac27](https://github.com/ClementBobin/BOB1-Api/commit/632ac2773237470b1377acf044c536f4a721a952))
* add user role management and location services ([9cdd563](https://github.com/ClementBobin/BOB1-Api/commit/9cdd5633fdae866ff57f62d7e9112e7527b468cd))
* added notif ([ad29974](https://github.com/ClementBobin/BOB1-Api/commit/ad29974c345a24e71e9242a9f667eea137a1ff81))
* enhance biometric authentication by updating login methods and token generation ([7e02dd3](https://github.com/ClementBobin/BOB1-Api/commit/7e02dd3f0965c7cd7027e0e3196384fed8d95faf))
* enhance Dockerfile for versioning and update GitHub Actions workflow for semantic release ([e51dc49](https://github.com/ClementBobin/BOB1-Api/commit/e51dc49b8c570c4874ad1332eea939493fd2c7cb))
* enhance match service with user context and add slot update functionality ([525255f](https://github.com/ClementBobin/BOB1-Api/commit/525255f537db33ad59fcff8ac8c3f62d0f599b0c))
* implement biometric authentication methods in AuthService and related interfaces ([65854b6](https://github.com/ClementBobin/BOB1-Api/commit/65854b6e201dd39d0b1fdf8fd5968a77ff24649d))
* implement Play Integrity verification service and controller ([d01c039](https://github.com/ClementBobin/BOB1-Api/commit/d01c03910621f4440a39778b090a7ec8c9f612da))
* implement role-based access control by introducing UserRoleMapping entity and updating user roles handling ([86a4254](https://github.com/ClementBobin/BOB1-Api/commit/86a42541a5394a30add0de5233e95cd9eda774e8))
* implement SeasonPoint service and controller for user point management ([d0ee6d2](https://github.com/ClementBobin/BOB1-Api/commit/d0ee6d2d930b45e6f48a77422235b00e99808f87))
* implement user role management with mapping entity and update user model ([e5ba15e](https://github.com/ClementBobin/BOB1-Api/commit/e5ba15e9458e1487d26b9f5dbe06dc1bc5eb8b82))
* init ([9ae2a73](https://github.com/ClementBobin/BOB1-Api/commit/9ae2a731121ed6e5ff2b20cadd67bacb03a7f9bf))
* init all feature ([efe0d2a](https://github.com/ClementBobin/BOB1-Api/commit/efe0d2a5d91f1767cab53c4d89ebf80a81abb4d2))
* refactor match service to use MinMatchDto for optimized data retrieval ([4677062](https://github.com/ClementBobin/BOB1-Api/commit/46770620659356761817c1dc4148b6d0223b7f75))
* refine Dockerfile for improved build process and debugging ([195cd29](https://github.com/ClementBobin/BOB1-Api/commit/195cd29cef3e0d3f7efd0d6c7b2ea216da2a092d))
* update biometric token generation to return a structured response ([e839a7b](https://github.com/ClementBobin/BOB1-Api/commit/e839a7b60f4eb534d625c07ca42882f8737c9e39))
* update Docker configuration for Vercel deployment and modify Program.cs for port handling ([a36caaf](https://github.com/ClementBobin/BOB1-Api/commit/a36caaf72cef0b38993d128c5336730cb0a39344))
* update Dockerfile for Vercel deployment and adjust build context ([2d0dba1](https://github.com/ClementBobin/BOB1-Api/commit/2d0dba1af3bdbcf062c5e2a9e86fe7d4acd5d6f2))
* update health check endpoint to /api/health ([6570031](https://github.com/ClementBobin/BOB1-Api/commit/6570031d1de9a2d209534fd07f16309cbd6ed02d))
* update launchSettings.json to use port instead of applicationUrl and comment out debug command in Dockerfile ([6fd0261](https://github.com/ClementBobin/BOB1-Api/commit/6fd026171821344005660b37a3b8c2404c234a65))
* update user roles management by introducing UserRoles table and adding BiometricToken to Users ([ccc6cae](https://github.com/ClementBobin/BOB1-Api/commit/ccc6cae49bd1678cba47e5087ec51fb86b2c7f49))

### 🐛 Bug Fixes

* change database provider from sqlite to postgres in development settings ([7f090f1](https://github.com/ClementBobin/BOB1-Api/commit/7f090f1b3c7fd72a71d1da9883a1c118c359eac6))
* change URL binding to HTTP and add health check endpoint ([32d0bf3](https://github.com/ClementBobin/BOB1-Api/commit/32d0bf3dbe3120ff4ce4f8f63946e61f24abc4a9))
* Install missing Kerberos library in Dockerfile ([7bb87d9](https://github.com/ClementBobin/BOB1-Api/commit/7bb87d924a51ad52edeb98aca233bfa708839fcc))
* specify version for conventional-changelog-conventionalcommits plugin in GitHub Actions workflow ([4ee2066](https://github.com/ClementBobin/BOB1-Api/commit/4ee206646c2a09b34c412270200b5f56da0f9ff1))
* uncomment debugging command to verify published API DLL in Dockerfile ([f7a8477](https://github.com/ClementBobin/BOB1-Api/commit/f7a8477b689c37741a2cc00e0ed210e32c5d4476))
* update default port from 80 to 8080 in Program.cs ([43a423b](https://github.com/ClementBobin/BOB1-Api/commit/43a423b0e08f138c8482236f25d3b397cbff8e08))
* Update Dockerfile.vercel ([8c75275](https://github.com/ClementBobin/BOB1-Api/commit/8c7527591b0f4b4723b64dc3a68a7968ced0f2b7))
* update project references and clean up Dockerfile for improved build process ([de92d46](https://github.com/ClementBobin/BOB1-Api/commit/de92d46550642851f50a6c9de657444a631ca9b8))
* update URL binding to allow connections from all interfaces and improve startup error logging ([551b6d6](https://github.com/ClementBobin/BOB1-Api/commit/551b6d6c2a6ca18732ae81dc288a580d4be64183))
* update URL binding to use HTTPS and set default connection string for development ([eb4f26f](https://github.com/ClementBobin/BOB1-Api/commit/eb4f26fe56d70d3a0e3a2688bee0c425880a200f))

### ♻️ Refactoring

* comment out CORS policy configuration in Program.cs ([71570c5](https://github.com/ClementBobin/BOB1-Api/commit/71570c5ca348ad0fba449fb4c060698e81cf140f))
* improve code formatting and structure in Program.cs ([1786650](https://github.com/ClementBobin/BOB1-Api/commit/1786650287a6df595d3757a7dee54593e665d8e0))
* improve error handling and response formatting in GlobalExceptionMiddleware ([ef72774](https://github.com/ClementBobin/BOB1-Api/commit/ef72774eac1c4d6b3a2e8e1f0c0e48a9ff53ef06))
