# Notifications supplémentaires à envisager – BOB1

Synthèse issue du croisement du document d'architecture (Notification Trigger Tree), de la todo « Notification » et de l'OpenAPI de Bob1.Api. Les candidats sont classés par valeur pour les utilisateurs.

## 1. À forte valeur (impact direct sur quelqu'un)

### Participation / postes

- Retrait d'un slot par un admin (DELETE /admin/slots/{id}) ou modification (PUT /admin/slots/{id} : changement d'horaire, de rôle, capacité à la baisse). Les inscrits ne sont pas prévenus aujourd'hui. Si la capacité tombe sous le nombre d'inscrits, il faut aussi décider qui est éjecté.
- Nouveau poste ouvert sur un match (POST /admin/matches/{id}/slots) : notifier le staff/bénévoles concernés. C'est le complément de l'item « staff si besoin de postes ».
- Poste urgent non pourvu : un UrgentQuery (days) existe déjà dans le schéma. Un rappel périodique du même type que PresenceReminderService (J-4 / J-1 sur les slots vides) serait cohérent.
- Rappel avant match pour les inscrits à un slot (pas seulement les familles en attente de confirmation).

### Covoiturage

- Réservation d'une place (POST /rides/{id}/booking) : le conducteur n'est pas prévenu.
- Annulation d'une réservation (DELETE /rides/{id}/booking) : le conducteur doit savoir qu'une place s'est libérée.
- Modification du trajet (PUT /rides/{id} : heure de départ, notes, places) : les passagers doivent être prévenus, surtout pour l'heure.
- Trajet qui devient complet (optionnel, faible priorité).

### Match

- Replanification : le TODO existe déjà. Cela inclut aussi un changement de lieu (venueId) ou d'équipe, pas seulement de date. Un diff « ancienne valeur → nouvelle valeur » dans le body serait utile.
- Match passé à « joué » : déjà noté. Utile pour déclencher les points, mais pas forcément une notification en soi.

## 2. Comptes et famille

- Invitation famille acceptée (POST /family/invitations puis register avec invitationCode) : notifier les membres existants qu'un nouveau membre a rejoint.
- Changement de famille d'une personne (PUT /admin/people/{id}, familyId) : notifier la personne concernée et les deux familles.
- Changement d'équipe/division : déjà prévu. Penser aussi au coach : quand TeamRequest.coachUserId change, notifier le nouveau coach et l'ancien.
- Mot de passe changé (PUT /me/password) : notification de sécurité envoyée à la personne elle-même. Souvent oubliée, et c'est un classique anti-compromission.
- Création de compte par un admin (POST /admin/people) : un mail/notification de bienvenue avec le mot de passe à changer, plutôt qu'un push.

## 3. Ranking / points

- Pénalité rapide (/penalties/quick) : même traitement que la pénalité normale. Vérifier que les deux chemins notifient.
- Modification d'un point existant (PUT /admin/points/{id}) et révocation : notifier la personne concernée, pas seulement les admins. La todo ne parle que des admins, alors que la personne voit son score changer sans explication.
- Seuils de classement : déjà dans le document d'architecture (Ranking Milestone).
- Changement de règle : les admins sont notifiés. Réfléchir à notifier tout le monde si un barème change (transparence), éventuellement via un message groupé plutôt qu'un push par règle.

## 4. Admin : alertes opérationnelles

- Suppression d'une division / équipe / lieu (DELETE) alors que des matchs ou des personnes y sont rattachés : alerte aux admins, et surtout à vérifier côté intégrité (cascade).
- Désactivation d'une division ou d'une équipe (isActive) : impact sur les joueurs et les matchs à venir.
- Notification admin modifiée ou supprimée (PUT/DELETE /admin/notifications/{id}) : si elle était recurring, les destinataires ne savent pas qu'elle a changé.
- Fin de saison / début de saison : « new season » est déjà prévu ; la fin de saison (récap, remise à zéro des points) en est le pendant naturel.

## 5. Technique (pas des notifications utilisateur, mais liées)

- Échecs FCM répétés : le push étant best-effort, loguer ne suffit pas. Un compteur de tokens invalides supprimés ou un taux d'échec anormal mérite une alerte admin.
- Utilisateur sans aucun push token : l'in-app existe mais le push n'arrive jamais. Un fallback email, déjà dans le document (« Email channel »), devient pertinent pour les cas critiques (match annulé).
- Préférences par type (étape 6 du document) : à faire avant d'ajouter trop de triggers, sinon risque de fatigue de notification.

## 6. Deux points de vigilance sur la todo actuelle

1. Notifier tous les admins à chaque action admin (points, règles, rôles, suspensions) peut devenir très bruyant. Suggestion : un type audit en in-app uniquement (sans push), ou un digest quotidien, et ne pas notifier l'admin qui a effectué l'action.

2. Déduplication : seul le rappel de présence dispose de reminder_logs. Dès l'ajout de rappels de postes urgents, généraliser cette table (clé : type + entité + destinataire) plutôt que d'en créer une par trigger.

## Suite possible

Un tableau consolidé (trigger, destinataires, canal, priorité, type de notification) fusionnant la todo et ces ajouts pourrait remplacer la section « Recipient Resolution Matrix » du document d'architecture.

## Compléments (2e passe)

Candidats supplémentaires, surtout des effets en cascade que la todo et la première liste ne couvrent pas.

### Cascades à partir d'actions existantes

- Match annulé → covoiturage : l'annulation laisse des trajets orphelins. Notifier les conducteurs et les passagers, et idéalement annuler automatiquement les trajets.
- Match replanifié → covoiturage : departureUtc risque d'être faux dès que l'heure ou le lieu change. Notifier conducteurs et passagers pour que le conducteur ajuste le trajet.
- Personne désactivée ou suspendue → ses engagements : ses inscriptions à des postes et ses réservations ou offres de trajets restent actifs. Notifier l'organisateur du match et les admins du trou, et les passagers si la personne était conductrice.
- Rôle retiré (staff, bénévole, etc.) : les inscriptions à des postes de ce rôle deviennent invalides. Notifier la personne et l'organisateur.
- Enfant déplacé de division ou d'équipe : ses inscriptions de présence en attente peuvent pointer vers de mauvais matchs. Notifier la famille, pas seulement le joueur.

### Pénalités (dérivé de pointsJ15 / pointsJ4 dans UpdateRuleRequest)

- Rappel de l'échéance de pénalité : les règles varient selon la proximité du match. Un message du type « vous pouvez encore vous désinscrire sans pénalité jusqu'à J-15 » ou « après J-4, vous perdez N points » évite les mauvaises surprises. Probablement plus utile que la notification de la pénalité elle-même.
- Désinscription tardive : quand quelqu'un quitte un poste dans la fenêtre J-4, prévenir immédiatement l'organisateur et les admins. C'est le cas urgent, et la vraie valeur du trigger « Slot Unfilled ».
- Pénalité appliquée automatiquement (par exemple une absence constatée quand le match passe à « joué ») : la personne doit recevoir le motif et le match, pas seulement une variation de points.

### Participation

- Liste d'attente : elle n'existe pas dans l'API aujourd'hui. Si elle est ajoutée, « une place s'est libérée » est probablement la notification à plus fort engagement.
- Confirmation d'inscription à un poste : un accusé de réception (« vous êtes inscrit comme X sur le match Y »), in-app uniquement. Faible priorité, mais peu coûteux.
- Rappel le jour du match pour les conducteurs et passagers (heure de départ), en plus des titulaires de postes.

### Admin et onboarding

- Nouvelle inscription via code d'invitation : notifier les admins (ou seulement la famille), surtout si familyName est nouveau.
- Code d'invitation créé ou sur le point d'expirer : notifier la famille qui l'a généré.
- Match sans joueurs dans une division ou une équipe : la todo prévoit de créer une ligne pour l'équipe sans joueur. Une alerte admin du type « match créé mais personne à notifier » rendrait ce manque visible.

### Sécurité

- Connexion depuis un nouvel appareil, ou refresh token révoqué / déconnexion partout : à associer à la notification de changement de mot de passe.

## Priorités recommandées

Si seulement trois ajouts devaient passer en premier : la cascade match annulé/replanifié → covoiturage, l'alerte de désinscription tardive à l'organisateur, et les rappels d'échéance de pénalité. Ils préviennent de vrais problèmes plutôt que de simplement informer.

## Décision : pas d'email, sauf sécurité du compte

L'application n'envoie aucune notification par email, à l'exception de celles liées à la sécurité du compte. Cette règle prime sur les passages précédents de ce document.

### Ce qui reste par email (sécurité du compte uniquement)

- Mot de passe changé (PUT /me/password).
- Connexion depuis un nouvel appareil, ou refresh token révoqué / déconnexion partout.
- Plus généralement tout événement qui touche l'accès au compte (réinitialisation de mot de passe, changement d'adresse email).

### Passages à corriger à la lumière de cette règle

- Création de compte par un admin (POST /admin/people) : pas d'email de bienvenue. L'admin transmet lui-même le mot de passe initial, et la personne est invitée à le changer à la première connexion (ce qui déclenche ensuite la notification de sécurité).
- Utilisateur sans push token : le fallback email n'est plus une option, y compris pour un match annulé. Le canal de repli est l'in-app, éventuellement avec un compteur ou une alerte admin pour les utilisateurs sans aucun token.
- Document d'architecture : le canal « Email » prévu pour Match Cancelled, Match Scheduled et l'escalade du rappel de présence (après 3 rappels), ainsi que l'étape « Implement email channel (SendGrid/SMTP) », sont à retirer ou à restreindre aux messages de sécurité.
- Canaux de toutes les autres notifications : Push + In-app uniquement.

## Autres possibilités (3e passe)

Nouveaux candidats, non couverts jusqu'ici :

- Présence confirmée par un proche (POST /matches/{id}/presence/confirm) : prévenir les autres membres de la famille pour éviter une double confirmation ou une confusion.
- Trajet proposé pour un match (POST /matches/{id}/rides) : informer les familles dont la présence est confirmée qu'un covoiturage existe.
- Rappel de trajet (heure de départ) pour le conducteur et les passagers.
- Pénalité non acquittée : relance quand l'écran d'acquittement au démarrage n'a pas été validé.
- Rappel d'échéance de pénalité (J-15 / J-4) avant une désinscription tardive.
- Changement de position au classement : digest hebdomadaire plutôt qu'un push à chaque variation.
- Code d'invitation sur le point d'expirer : prévenir la famille qui l'a généré.
- Match créé sans destinataires : alerte admin quand aucun joueur n'est rattaché à l'équipe concernée.
- Lieu d'entraînement d'une équipe modifié (trainingVenueId) : prévenir les joueurs et le coach.
- Liste d'attente (si elle est ajoutée) : « une place s'est libérée ».

## Tableau consolidé des notifications

Lecture des colonnes : Priorité (P1 critique, P2 haute, P3 normale, P4 basse), Clé (identifiant technique du trigger), Type (famille de notification, utile pour les préférences utilisateur), Variante (destinataires et variantes de message : admin, staff, joueur, famille, conducteur, passager, coach, personne concernée), Portée (périmètre de ciblage). Canal par défaut : push + in-app. Seules les notifications de type security passent aussi par email.

Canaux et niveaux : une notification peut combiner plusieurs canaux, mais elle passe toujours par l'in-app, quel que soit son type. Trois canaux, chacun avec un niveau : in-app = bas (toujours présent, consultable dans l'application), push = moyen (alerte sur l'appareil), email = haut (réservé à la sécurité du compte). Le niveau d'une notification est celui de son canal le plus élevé. Les copies d'audit destinées aux admins restent en in-app uniquement.

| Priorité | Clé | Type | Variante | Portée | Canaux (niveau) |
|---|---|---|---|---|---|
| P1 | match.cancelled | match | admin, staff, famille, conducteur, passager | match | in-app + push (moyen) |
| P1 | match.rescheduled | match | admin, staff, famille, conducteur, passager (diff ancien → nouveau) | match | in-app + push (moyen) |
| P3 | match.created | match | joueur, staff (si postes à pourvoir), admin | division, équipe | in-app + push (moyen) |
| P3 | match.reminder | match | staff, famille, conducteur, passager (J-1 / jour J) | match | in-app + push (moyen) |
| P4 | match.played | match | admin (déclenche les points) | match | in-app (bas) |
| P3 | match.no_recipients | match | admin | match | in-app (bas) |
| P2 | presence.reminder | participation | famille | match, famille | in-app + push (moyen) |
| P3 | presence.confirmed | participation | famille (autres membres) | famille | in-app (bas) |
| P2 | slot.created | participation | staff | match, rôle | in-app + push (moyen) |
| P2 | slot.updated | participation | inscrit, organisateur | poste | in-app + push (moyen) |
| P1 | slot.deleted | participation | inscrit, organisateur | poste | in-app + push (moyen) |
| P2 | slot.urgent_unfilled | participation | staff, admin | match (J-4 / J-1) | in-app + push (moyen) |
| P2 | slot.late_withdrawal | participation | organisateur, admin | poste (fenêtre J-4) | in-app + push (moyen) |
| P4 | slot.full_or_unfilled | participation | admin, organisateur | poste | in-app (bas) |
| P4 | slot.join_confirmation | participation | personne concernée | poste | in-app (bas) |
| P3 | waitlist.spot_available | participation | personne concernée (si liste d'attente) | poste | in-app + push (moyen) |
| P3 | ride.offered | carpool | famille (présence confirmée) | match | in-app (bas) |
| P2 | ride.booked | carpool | conducteur | trajet | in-app + push (moyen) |
| P3 | ride.booking_cancelled | carpool | conducteur | trajet | in-app + push (moyen) |
| P2 | ride.updated | carpool | passager | trajet | in-app + push (moyen) |
| P1 | ride.cancelled | carpool | passager | trajet | in-app + push (moyen) |
| P3 | ride.reminder | carpool | conducteur, passager | trajet | in-app + push (moyen) |
| P4 | ride.full | carpool | conducteur | trajet | in-app (bas) |
| P2 | person.team_division_changed | club | personne, famille, coach, admin | personne | in-app + push (moyen) |
| P2 | person.role_changed | compte | personne concernée, admin | personne | in-app + push (moyen) |
| P2 | person.status_changed (suspendu, désactivé, réactivé) | compte | personne concernée, admin, organisateur (engagements) | personne | in-app + push (moyen) |
| P3 | person.family_changed | compte | personne, familles concernées, admin | famille | in-app + push (moyen) |
| P3 | family.member_joined | compte | famille, admin | famille | in-app (bas) |
| P3 | team.coach_changed | club | ancien coach, nouveau coach, admin | équipe | in-app + push (moyen) |
| P3 | team.training_venue_changed | club | joueur, coach | équipe | in-app + push (moyen) |
| P4 | invitation.expiring | compte | famille | famille | in-app (bas) |
| P1 | security.password_changed | security | personne concernée | personne | in-app + push + email (haut) |
| P1 | security.new_device_login | security | personne concernée | personne | in-app + push + email (haut) |
| P1 | security.sessions_revoked | security | personne concernée | personne | in-app + push + email (haut) |
| P2 | points.adjusted (ajout, modification, révocation) | ranking | personne concernée, admin (audit) | personne | personne : in-app + push (moyen) ; admin : in-app (bas) |
| P2 | penalty.applied (manuelle, rapide, automatique) | ranking | personne, famille, admin (audit) | personne | personne, famille : in-app + push (moyen) ; admin : in-app (bas) |
| P2 | penalty.ack_pending | ranking | personne concernée | personne | in-app + push (moyen) |
| P3 | penalty.deadline_reminder | ranking | joueur, famille | match (J-15 / J-4) | in-app + push (moyen) |
| P3 | ranking.rule_changed | ranking | admin, tous (message groupé) | tous | in-app (bas) |
| P4 | ranking.milestone | ranking | personne, famille | personne, famille | in-app (bas) |
| P4 | ranking.position_digest | ranking | personne concernée (hebdomadaire) | tous, division | in-app (bas) |
| P3 | broadcast.sent | broadcast | destinataires, admin (copie si absent des destinataires) | tous, division, équipe (TeamId à implémenter) | destinataires : in-app + push (moyen) ; admin : in-app (bas) |
| P4 | broadcast.updated_or_deleted | broadcast | destinataires (surtout si recurring) | tous, division, équipe | in-app (bas) |
| P3 | season.started | système | tous | tous | in-app + push (moyen) |
| P3 | season.ended | système | tous (récap, remise à zéro des points) | tous | in-app + push (moyen) |
| P2 | admin.entity_deleted_or_deactivated (division, équipe, lieu) | club | admin | division, équipe, lieu | in-app (bas) |
| P2 | system.push_failure_rate | système | admin | système | in-app (bas) |
| P4 | system.no_push_token | système | admin | personne | in-app (bas) |

Notes d'application :

- Les copies destinées aux admins (audit) sont à envoyer en in-app uniquement, sans push, et sans notifier l'admin qui a déclenché l'action.
- Les types (colonne Type) servent de clé pour les préférences utilisateur (opt-in / opt-out), sauf security, qui reste toujours active.
- Les rappels (*.reminder, slot.urgent_unfilled, penalty.deadline_reminder) doivent partager une table de déduplication commune (clé : type + entité + destinataire).

## Autres possibilités (4e passe)

Même format que le tableau consolidé (Priorité, Clé, Type, Variante, Portée). Ces lignes viennent en complément du tableau précédent.

| Priorité | Clé | Type | Variante | Portée | Canaux (niveau) |
|---|---|---|---|---|---|
| P2 | presence.withdrawn (présence annulée par une famille) | participation | coach, conducteur et passagers concernés (réservation à libérer), admin si pénalité | match, famille | in-app + push (moyen) |
| P3 | presence.summary (récap J-2 : confirmés / en attente / absents) | participation | coach | équipe, match | in-app (bas) |
| P3 | slot.conflict (deux postes ou deux matchs qui se chevauchent) | participation | personne concernée | personne | in-app (bas) |
| P2 | match.venue_conflict (deux matchs au même lieu, créneaux qui se chevauchent) | match | admin | lieu, match | in-app + push (moyen) |
| P3 | ride.no_passenger (départ proche, aucune réservation) | carpool | conducteur | trajet | in-app (bas) |
| P1 | security.email_changed (envoyé à l'ancienne adresse) | security | personne concernée | personne | in-app + push + email (haut) |
| P1 | security.password_reset_by_admin | security | personne concernée | personne | in-app + push + email (haut) |
| P2 | person.participation_suspended / person.participation_restored | compte | personne, famille, admin | personne | in-app + push (moyen) |
| P3 | penalty.suspension_threshold_warning (proche du seuil de suspension) | ranking | personne, famille | personne | in-app + push (moyen) |
| P3 | person.unassigned (compte sans famille ni division, donc sans notification) | compte | admin | personne | in-app (bas) |
| P4 | family.member_removed | compte | famille, personne concernée, admin | famille | in-app (bas) |
| P4 | broadcast.expired (notification récurrente ou « startup » arrivée à expiration) | broadcast | admin auteur | tous, division, équipe | in-app (bas) |
| P3 | system.maintenance (fenêtre de maintenance annoncée) | système | tous | tous | in-app + push (moyen) |
| P2 | system.app_update_required (version minimale de l'app non respectée) | système | tous (au démarrage) | tous | in-app (bas) |
| P4 | digest.weekly_family (matchs à venir, présences en attente, trajets ouverts) | digest | famille | famille | in-app + push (moyen) |
| P4 | digest.weekly_admin (audit des actions, alertes non résolues) | digest | admin | tous | in-app (bas) |

## Aspects transverses (pas des triggers, mais qui conditionnent leur qualité)

- Digest en remplacement : un récapitulatif hebdomadaire pour les familles et pour les admins permet de supprimer plusieurs notifications de priorité basse (P3 / P4) et réduit la fatigue.
- Heures calmes : pas de push la nuit pour les priorités normale et basse, avec report au matin. Les P1 passent toujours.
- Lien profond (deep link) : chaque notification devrait porter dans data la cible à ouvrir (match, trajet, poste, écran des pénalités), sinon le clic ne mène nulle part.
- Expiration des notifications sensibles au temps : un rappel ou une alerte de poste ne doit plus apparaître après le début du match (utiliser expiresAtUtc, déjà présent pour les diffusions admin).
- Regroupement : pour les notifications répétées sur une même entité (par exemple plusieurs réservations sur un même trajet), utiliser une clé de regroupement (collapse key FCM) plutôt qu'un push par événement.
- Fiabilité d'envoi : puisque le push est best-effort, passer par une table d'envoi (outbox) avec nouvelle tentative évite de perdre une notification critique (annulation de match, par exemple) lors d'une panne ponctuelle.
- Limitation de débit des diffusions admin : éviter qu'une diffusion « tous les membres » répétée ne sature les utilisateurs.

## État d'implémentation (branche `feat/notifications`)

Règles appliquées partout : canaux push + in-app, sauf les copies d'audit admin (in-app seulement, sans l'admin qui a agi) ; aucun email.
Les notifications sont envoyées après l'enregistrement et ne font jamais échouer l'action de l'admin (erreurs journalisées).

**Fait**

| Clé | Où |
|---|---|
| match.created (joueurs + familles des deux équipes, alerte admin si personne, copie admin) | Notifications / `MatchCreatedHandler` |
| match.cancelled (familles confirmées, inscrits, joueurs, staff si postes staff, copie admin) | Participation / `MatchCancelledHandler` |
| match.rescheduled (diff date / lieu / équipes) | Participation / `MatchRescheduledHandler` |
| cascade match annulé → covoiturages supprimés + conducteurs/passagers prévenus (sans pénalité conducteur) | Carpool / `MatchCancelledRidesHandler` |
| cascade match replanifié → conducteurs/passagers | Carpool / `MatchRescheduledRidesHandler` |
| slot.created (staff, ou parents des équipes du match) | `SlotsAdminController.Create` |
| slot.updated (inscrits) et slot.deleted (inscrits ; la suppression est maintenant autorisée avec inscrits, sans pénalité) | `SlotsAdminController` |
| slot.late_withdrawal (fenêtre J-4, admins) et slot.join_confirmation (in-app) | `SlotMembershipHandler` |
| ride.booked, ride.booking_cancelled, ride.updated | `CarpoolController` |
| person.team_division_changed, person.family_changed, person.role_changed, person.status_changed, person.participation_suspended / restored | Identity / `PeopleNotices` |
| family.member_joined (in-app) | `AuthService.RegisterAsync` |
| security.password_changed (in-app + push ; pas d'email car aucun envoi email n'existe) | `MeController` |
| points.adjusted (ajout, modification, révocation), penalty.applied (+ famille), ranking.rule_changed (message groupé in-app) | `RankingAdminController` |
| team.coach_changed, team.training_venue_changed, désactivation / suppression division, équipe, lieu (alertes admin) | `ClubAdminController` |
| season.started | `SeasonService` |
| broadcast.sent : ciblage par équipe (`TeamId`) + copie in-app aux admins absents des destinataires | `NotificationsAdminController` |

**Pas fait (à planifier)** : rappels périodiques (slot.urgent_unfilled, match.reminder, ride.reminder, penalty.deadline_reminder, penalty.ack_pending, presence.summary), digests (position au classement, familles, admin), ranking.milestone, season.ended, liste d'attente, présence confirmée/annulée par un proche, ride.offered / ride.no_passenger / ride.full, conflits (slot.conflict, match.venue_conflict), pénalité automatique, invitation.expiring, security.new_device_login / sessions_revoked / email_changed / password_reset_by_admin et l'envoi d'email de sécurité, préférences par type, heures calmes, deep links normalisés, expiration, collapse key FCM, outbox avec reprise, table de déduplication commune, alertes système (échecs FCM, sans token, version minimale), broadcast.updated/deleted/expired.

