import 'package:flutter/material.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/item_type.dart';
import '../../app/theme/item_type_colors.dart';
import '../../l10n/generated/app_localizations.dart';

IconData itemTypeIcon(ItemType type) => switch (type) {
  ItemType.folder => FluentIcons.folder_24_regular,
  ItemType.image => FluentIcons.image_24_regular,
  ItemType.video => FluentIcons.video_24_regular,
  ItemType.audio => FluentIcons.music_note_2_24_regular,
  ItemType.pdf => FluentIcons.document_pdf_24_regular,
  ItemType.document => FluentIcons.document_text_24_regular,
  ItemType.spreadsheet => FluentIcons.document_table_24_regular,
  ItemType.presentation => FluentIcons.slide_text_24_regular,
  ItemType.archive => FluentIcons.folder_zip_24_regular,
  ItemType.text => FluentIcons.document_one_page_24_regular,
  ItemType.other || ItemType.$unknown => FluentIcons.document_24_regular,
};

String itemTypeLabel(ItemType type, AppLocalizations l10n) => switch (type) {
  ItemType.folder => l10n.itemTypeFolder,
  ItemType.image => l10n.itemTypeImage,
  ItemType.video => l10n.itemTypeVideo,
  ItemType.audio => l10n.itemTypeAudio,
  ItemType.pdf => l10n.itemTypePdf,
  ItemType.document => l10n.itemTypeDocument,
  ItemType.spreadsheet => l10n.itemTypeSpreadsheet,
  ItemType.presentation => l10n.itemTypePresentation,
  ItemType.archive => l10n.itemTypeArchive,
  ItemType.text => l10n.itemTypeText,
  ItemType.other || ItemType.$unknown => l10n.itemTypeOther,
};

class ItemTypeIcon extends StatelessWidget {
  const ItemTypeIcon(this.type, {super.key, this.size});

  final ItemType type;
  final double? size;

  @override
  Widget build(BuildContext context) =>
      Icon(itemTypeIcon(type), size: size, color: ItemTypeColors.on(context).of(type));
}
