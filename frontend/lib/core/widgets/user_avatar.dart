import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_providers.dart';
import 'picture_revision.dart';

class UserAvatar extends ConsumerWidget {
  const UserAvatar({
    super.key,
    required this.userId,
    required this.name,
    required this.hasPicture,
    required this.radius,
    this.pictureUri,
  });

  final String? userId;
  final String name;
  final bool hasPicture;
  final double radius;
  final Uri? pictureUri;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final initial = CircleAvatar(
      radius: radius,
      child: Text(name.characters.firstOrNull?.toUpperCase() ?? '', style: TextStyle(fontSize: radius * 0.9)),
    );
    if (!hasPicture) {
      return initial;
    }
    final id = userId;
    final picture =
        pictureUri ??
        (id == null
            ? null
            : Uri.parse(ref.watch(apiBaseUrlProvider)).replace(pathSegments: ['api', 'users', id, 'picture']));
    if (picture == null) {
      return initial;
    }
    final revision = ref.watch(pictureRevisionProvider);
    final url = picture.replace(queryParameters: revision == 0 ? null : {PictureRevision.parameter: '$revision'});
    return ClipOval(
      child: Image.network(
        url.toString(),
        width: radius * 2,
        height: radius * 2,
        fit: BoxFit.cover,
        webHtmlElementStrategy: WebHtmlElementStrategy.fallback,
        errorBuilder: (_, _, _) => initial,
      ),
    );
  }
}
