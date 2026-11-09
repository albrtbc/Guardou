import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../app/theme/app_tokens.dart';
import '../../core/widgets/brand_mark.dart';
import '../../core/widgets/source_code_link.dart';
import '../../l10n/generated/app_localizations.dart';

class SharedBrand extends StatelessWidget {
  const SharedBrand({super.key});

  @override
  Widget build(BuildContext context) => const Center(child: BrandMark());
}

class SharedLoadingPage extends StatelessWidget {
  const SharedLoadingPage({super.key});

  @override
  Widget build(BuildContext context) => Scaffold(
    body: Center(child: CircularProgressIndicator(semanticsLabel: AppLocalizations.of(context).loading)),
  );
}

class SharedLinkError extends StatelessWidget {
  const SharedLinkError({super.key, required this.error, required this.onRetry});

  static const _iconSize = 64.0;
  static const _notFound = 404;

  final Object error;
  final VoidCallback onRetry;

  bool get _unavailable => error is DioException && (error as DioException).response?.statusCode == _notFound;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final spacing = AppTokens.of(context).spacing;

    return Scaffold(
      appBar: AppBar(leading: const SharedBrand()),
      bottomNavigationBar: const SourceCodeLink(),
      body: Center(
        child: SingleChildScrollView(
          padding: EdgeInsets.all(spacing),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(
                _unavailable ? FluentIcons.link_dismiss_24_regular : FluentIcons.error_circle_24_regular,
                size: _iconSize,
              ),
              SizedBox(height: spacing),
              Text(
                _unavailable ? l10n.sharedLinkUnavailable : l10n.genericError,
                style: Theme.of(context).textTheme.titleMedium,
                textAlign: TextAlign.center,
              ),
              if (!_unavailable) ...[
                SizedBox(height: spacing),
                FilledButton(onPressed: onRetry, child: Text(l10n.errorRetry)),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
