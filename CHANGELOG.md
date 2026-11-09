# Changelog

## [0.11.0](https://github.com/GuardouApp/Guardou/compare/v0.10.0...v0.11.0) (2026-11-09)


### ⚠ BREAKING CHANGES

* rename Recommended to Suggested and move the item marks next to the name ([#56](https://github.com/GuardouApp/Guardou/issues/56))
* rename Home to Recommended and add the start page preference ([#55](https://github.com/GuardouApp/Guardou/issues/55))

### Features

* add storage quotas ([#53](https://github.com/GuardouApp/Guardou/issues/53)) ([aa6fed7](https://github.com/GuardouApp/Guardou/commit/aa6fed7cda332ad61f70834e2058dd1a7cd510c8))
* rename Home to Recommended and add the start page preference ([#55](https://github.com/GuardouApp/Guardou/issues/55)) ([605c3fb](https://github.com/GuardouApp/Guardou/commit/605c3fbdd3923a61faca0cf128aff299a1707b67))
* rename Recommended to Suggested and move the item marks next to the name ([#56](https://github.com/GuardouApp/Guardou/issues/56)) ([31bed4b](https://github.com/GuardouApp/Guardou/commit/31bed4ba27ae7f32f25cdf232f25bc8e8bc2d4ea))

## [0.10.0](https://github.com/GuardouApp/Guardou/compare/v0.9.1...v0.10.0) (2026-11-05)


### Features

* add resumable uploads with tusdotnet ([#51](https://github.com/GuardouApp/Guardou/issues/51)) ([a793fcc](https://github.com/GuardouApp/Guardou/commit/a793fccfaf235a2b93531ea29ad7a95632c8a94b))
* add user preferences ([#49](https://github.com/GuardouApp/Guardou/issues/49)) ([279a75f](https://github.com/GuardouApp/Guardou/commit/279a75fea3c4e50a359691312ef586cb51bc6de8))

## [0.9.1](https://github.com/GuardouApp/Guardou/compare/v0.9.0...v0.9.1) (2026-10-29)


### Bug Fixes

* update Flutter to 3.47.8 to remove the compromised eMudhra intermediates ([#44](https://github.com/GuardouApp/Guardou/issues/44)) ([ed76bdd](https://github.com/GuardouApp/Guardou/commit/ed76bdd1bc2ef06b9f087c44db5b88fb7659138d))

## [0.9.0](https://github.com/GuardouApp/Guardou/compare/v0.8.0...v0.9.0) (2026-10-29)


### Features

* add a shared indicator to items shared by me ([#43](https://github.com/GuardouApp/Guardou/issues/43)) ([69e920f](https://github.com/GuardouApp/Guardou/commit/69e920f37c8fa258759cf3498a84bf38d024eef5))
* add starred items ([#41](https://github.com/GuardouApp/Guardou/issues/41)) ([3e8a54b](https://github.com/GuardouApp/Guardou/commit/3e8a54b95ec1cdc569f0dae28bbd7804ac7343cc))

## [0.8.0](https://github.com/GuardouApp/Guardou/compare/v0.7.0...v0.8.0) (2026-10-27)


### ⚠ BREAKING CHANGES

* add environment overrides, trash retention, and the host time zone ([#40](https://github.com/GuardouApp/Guardou/issues/40))

### Features

* add environment overrides, trash retention, and the host time zone ([#40](https://github.com/GuardouApp/Guardou/issues/40)) ([56ede4d](https://github.com/GuardouApp/Guardou/commit/56ede4d612f466547fe86fb22afc05317bf63ac7))
* add search with a live search bar, filters, and folder sizes ([#34](https://github.com/GuardouApp/Guardou/issues/34)) ([eda4775](https://github.com/GuardouApp/Guardou/commit/eda477568243c472c7a5d89bd709790fdfcbca54))
* rebuild the search index from the task queue ([#38](https://github.com/GuardouApp/Guardou/issues/38)) ([6ccc8aa](https://github.com/GuardouApp/Guardou/commit/6ccc8aa6796bddef0d3693a9da0ab0a21a7d4af2))


### Bug Fixes

* **frontend:** load CanvasKit from the app, not from the CDN ([#37](https://github.com/GuardouApp/Guardou/issues/37)) ([3bd5a19](https://github.com/GuardouApp/Guardou/commit/3bd5a19c395a3fe15cee52979a2b64ab1eeed1f9))
* **frontend:** load the full CanvasKit variant ([#36](https://github.com/GuardouApp/Guardou/issues/36)) ([e8db66c](https://github.com/GuardouApp/Guardou/commit/e8db66c9cd16ff9f0d73b1bc91d4bb47578e3dfe))
* require an HTTPS issuer for OIDC ([#39](https://github.com/GuardouApp/Guardou/issues/39)) ([74d8db0](https://github.com/GuardouApp/Guardou/commit/74d8db0a4dce7f50b938cc76642b5ac6552c404f))

## [0.7.0](https://github.com/GuardouApp/Guardou/compare/v0.6.0...v0.7.0) (2026-10-27)


### Features

* direct share with users ([#28](https://github.com/GuardouApp/Guardou/issues/28)) ([c21eee5](https://github.com/GuardouApp/Guardou/commit/c21eee53e81d64f97f2691e5b90f79faec66e13d))
* show recent folders and files on home ([#33](https://github.com/GuardouApp/Guardou/issues/33)) ([3b17e12](https://github.com/GuardouApp/Guardou/commit/3b17e120a49b6e94c6217195bdb3cecd000f579e))
* show the last event on file cards ([#32](https://github.com/GuardouApp/Guardou/issues/32)) ([1eb7d7f](https://github.com/GuardouApp/Guardou/commit/1eb7d7f389ff3c82a8025d97abc7c936f4c1ec8a))

## [0.6.0](https://github.com/GuardouApp/Guardou/compare/v0.5.0...v0.6.0) (2026-10-24)


### ⚠ BREAKING CHANGES

* change the default port from 8080 to 2286
Guardou now listens on port 2286. Update your port mappings and reverse proxy settings.

### Features

* add support for sharing files by link ([#21](https://github.com/GuardouApp/Guardou/issues/21)) ([93e5eac](https://github.com/GuardouApp/Guardou/commit/93e5eacc3714319eca7b69db64a189040c20af00))
* add support for sharing folders by link ([#25](https://github.com/GuardouApp/Guardou/issues/25)) ([93e5eac](https://github.com/GuardouApp/Guardou/commit/93e5eacc3714319eca7b69db64a189040c20af00))
* change the default port from 8080 to 2286 ([93e5eac](https://github.com/GuardouApp/Guardou/commit/93e5eacc3714319eca7b69db64a189040c20af00))
* use the full screen on mobile browsers ([#22](https://github.com/GuardouApp/Guardou/issues/22)) ([93e5eac](https://github.com/GuardouApp/Guardou/commit/93e5eacc3714319eca7b69db64a189040c20af00))


### Bug Fixes

* fix the profile picture upload on iOS Safari ([#23](https://github.com/GuardouApp/Guardou/issues/23)) ([93e5eac](https://github.com/GuardouApp/Guardou/commit/93e5eacc3714319eca7b69db64a189040c20af00))
* remove the tap highlight on Android ([#24](https://github.com/GuardouApp/Guardou/issues/24)) ([93e5eac](https://github.com/GuardouApp/Guardou/commit/93e5eacc3714319eca7b69db64a189040c20af00))
* stop the info panel opening after closing the preview of an object ([#12](https://github.com/GuardouApp/Guardou/issues/12)) ([93e5eac](https://github.com/GuardouApp/Guardou/commit/93e5eacc3714319eca7b69db64a189040c20af00))
