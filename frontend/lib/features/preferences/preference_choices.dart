import 'package:flutter/material.dart';

import '../../api/generated/models/conflict_policy.dart';
import '../../api/generated/models/language.dart';
import '../../api/generated/models/start_page.dart';
import '../../api/generated/models/theme_preference.dart';
import '../../api/generated/models/upload_conflict_policy.dart';
import '../../app/router/routes.dart';
import '../../l10n/generated/app_localizations.dart';

extension ThemePreferenceChoice on ThemePreference {
  ThemeMode get mode => switch (this) {
    ThemePreference.light => ThemeMode.light,
    ThemePreference.dark => ThemeMode.dark,
    ThemePreference.system || ThemePreference.$unknown => ThemeMode.system,
  };

  String label(AppLocalizations l10n) => switch (this) {
    ThemePreference.light => l10n.themeLight,
    ThemePreference.dark => l10n.themeDark,
    ThemePreference.system || ThemePreference.$unknown => l10n.themeSystem,
  };
}

extension LanguageChoice on Language {
  Locale? get locale => switch (this) {
    Language.en => const Locale('en'),
    Language.pt => const Locale('pt'),
    Language.ptBr => const Locale('pt', 'BR'),
    Language.system || Language.$unknown => null,
  };

  String label(AppLocalizations l10n) => switch (this) {
    Language.en => l10n.languageEnglish,
    Language.pt => l10n.languagePortuguese,
    Language.ptBr => l10n.languagePortugueseBrazil,
    Language.system || Language.$unknown => l10n.languageSystem,
  };
}

extension UploadConflictPolicyChoice on UploadConflictPolicy {
  ConflictPolicy? get policy => switch (this) {
    UploadConflictPolicy.replace => ConflictPolicy.replace,
    UploadConflictPolicy.rename => ConflictPolicy.rename,
    UploadConflictPolicy.skip => ConflictPolicy.skip,
    UploadConflictPolicy.ask || UploadConflictPolicy.$unknown => null,
  };

  String label(AppLocalizations l10n) => switch (this) {
    UploadConflictPolicy.replace => l10n.conflictReplace,
    UploadConflictPolicy.rename => l10n.conflictRename,
    UploadConflictPolicy.skip => l10n.conflictSkip,
    UploadConflictPolicy.ask || UploadConflictPolicy.$unknown => l10n.uploadConflictAsk,
  };
}

extension StartPageChoice on StartPage {
  String get path => switch (this) {
    StartPage.suggested => Routes.suggested,
    StartPage.starred => Routes.starred,
    StartPage.shared => Routes.shared,
    StartPage.files || StartPage.$unknown => Routes.files,
  };

  String label(AppLocalizations l10n) => switch (this) {
    StartPage.suggested => l10n.navigationSuggested,
    StartPage.starred => l10n.navigationStarred,
    StartPage.shared => l10n.navigationSharedWithMe,
    StartPage.files || StartPage.$unknown => l10n.navigationFiles,
  };
}
