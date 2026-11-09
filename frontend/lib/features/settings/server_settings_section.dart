import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../api/generated/models/server_settings.dart';
import '../../api/generated/models/server_settings_view.dart';
import '../../l10n/generated/app_localizations.dart';
import 'server_settings_controller.dart';
import 'settings_section.dart';

class ServerSettingsSection extends ConsumerWidget {
  const ServerSettingsSection({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    return SettingsSection(
      icon: LucideIcons.globe,
      title: l10n.serverSettings,
      description: l10n.serverSettingsDescription,
      value: ref.watch(serverSettingsProvider),
      onRetry: () => ref.invalidate(serverSettingsProvider),
      builder: (settings) => _ServerSettingsForm(settings: settings),
    );
  }
}

class _ServerSettingsForm extends ConsumerStatefulWidget {
  const _ServerSettingsForm({required this.settings});

  final ServerSettingsView settings;

  @override
  ConsumerState<_ServerSettingsForm> createState() => _ServerSettingsFormState();
}

class _ServerSettingsFormState extends ConsumerState<_ServerSettingsForm>
    with SettingsForm<_ServerSettingsForm, ServerSettingsView> {
  static const _minimumRetention = 1;
  static const _maximumRetention = 3650;
  static const _minimumConcurrency = 1;
  static const _maximumConcurrency = 6;
  static const _minimumChunkSize = 1;
  static const _maximumChunkSize = 90;
  static const _minimumFileSize = 1;
  static const _minimumFreeSpace = 0;
  static const _maximumStorage = 1048576;

  final _externalUrl = TextEditingController();
  final _trashRetention = TextEditingController();
  final _uploadConcurrency = TextEditingController();
  final _uploadChunkSize = TextEditingController();
  final _maxFileSize = TextEditingController();
  final _minFreeSpace = TextEditingController();

  @override
  ServerSettingsView get saved => widget.settings;

  @override
  void initState() {
    super.initState();
    fill(widget.settings);
  }

  @override
  void fill(ServerSettingsView value) {
    _externalUrl.text = value.externalUrl ?? '';
    _trashRetention.text = '${value.trashRetentionDays}';
    _uploadConcurrency.text = '${value.uploadConcurrency}';
    _uploadChunkSize.text = '${value.uploadChunkSizeMb}';
    _maxFileSize.text = '${value.maxFileSizeMb ?? ''}';
    _minFreeSpace.text = '${value.minFreeSpaceMb}';
  }

  @override
  Future<ServerSettingsView> defaults() => ref.read(serverSettingsProvider.notifier).defaults();

  @override
  Future<void> persist() => ref
      .read(serverSettingsProvider.notifier)
      .save(
        ServerSettings(
          externalUrl: trimmedOrNull(_externalUrl.text),
          trashRetentionDays: int.parse(_trashRetention.text),
          uploadConcurrency: int.parse(_uploadConcurrency.text),
          uploadChunkSizeMb: int.parse(_uploadChunkSize.text),
          maxFileSizeMb: int.tryParse(_maxFileSize.text),
          minFreeSpaceMb: int.parse(_minFreeSpace.text),
        ),
      );

  @override
  void dispose() {
    _externalUrl.dispose();
    _trashRetention.dispose();
    _uploadConcurrency.dispose();
    _uploadChunkSize.dispose();
    _maxFileSize.dispose();
    _minFreeSpace.dispose();
    super.dispose();
  }

  String? _validateRange(AppLocalizations l10n, String? value, int minimum, int maximum, {bool optional = false}) {
    if (optional && (value ?? '').isEmpty) {
      return null;
    }
    final number = int.tryParse(value ?? '');
    final valid = number != null && number >= minimum && number <= maximum;
    return valid ? null : l10n.concurrencyInvalid(minimum, maximum);
  }

  Widget _numberField({
    required String label,
    required String description,
    required TextEditingController controller,
    required String field,
    required int minimum,
    required int maximum,
    bool optional = false,
  }) => SettingsTextField(
    label: label,
    description: description,
    required: !optional,
    controller: controller,
    keyboardType: TextInputType.number,
    inputFormatters: [FilteringTextInputFormatter.digitsOnly],
    lockedBy: lockedVariable(widget.settings.locked, field),
    validator: (value) => _validateRange(AppLocalizations.of(context), value, minimum, maximum, optional: optional),
  );

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);

    return Form(
      key: formKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SettingsTextField(
            label: l10n.externalUrl,
            description: l10n.externalUrlDescription,
            controller: _externalUrl,
            keyboardType: TextInputType.url,
            lockedBy: lockedVariable(widget.settings.locked, 'externalUrl'),
            validator: (value) => validateHttpUrl(l10n, value),
          ),
          _numberField(
            label: l10n.trashRetentionDays,
            description: l10n.trashRetentionDescription,
            controller: _trashRetention,
            field: 'trashRetentionDays',
            minimum: _minimumRetention,
            maximum: _maximumRetention,
          ),
          _numberField(
            label: l10n.uploadConcurrency,
            description: l10n.uploadConcurrencyDescription,
            controller: _uploadConcurrency,
            field: 'uploadConcurrency',
            minimum: _minimumConcurrency,
            maximum: _maximumConcurrency,
          ),
          _numberField(
            label: l10n.uploadChunkSize,
            description: l10n.uploadChunkSizeDescription,
            controller: _uploadChunkSize,
            field: 'uploadChunkSizeMb',
            minimum: _minimumChunkSize,
            maximum: _maximumChunkSize,
          ),
          _numberField(
            label: l10n.maxFileSize,
            description: l10n.maxFileSizeDescription,
            controller: _maxFileSize,
            field: 'maxFileSizeMb',
            minimum: _minimumFileSize,
            maximum: _maximumStorage,
            optional: true,
          ),
          _numberField(
            label: l10n.minFreeSpace,
            description: l10n.minFreeSpaceDescription,
            controller: _minFreeSpace,
            field: 'minFreeSpaceMb',
            minimum: _minimumFreeSpace,
            maximum: _maximumStorage,
          ),
          settingsActions(),
        ],
      ),
    );
  }
}
