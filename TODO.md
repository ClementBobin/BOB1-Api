# Notification

- [] crée un match devrait notifier les joueur si il y en a (si aucun joureur ne crée pas de nouvelle row for the team that has no player), and should send a notif to all with staff role (if there is need for postes) and send a notif to all admin
- [] same thing for annuler le match
- [] player changing team et/or divisions need to be send a notification stating change, admin notifié
- [] suspension et/or status du compte d'une personne same thing (for disable/enable), admin notifié
- [] changement de role aussi, admin notifié
- [] ajout de point ou révocation ou modification envoie une notif à tous les admins
- [] règles des points envoie une notif à tous les admins du changements
- [] notification à la main doit envoyer aussi une notification au admin a moins qu'il sont dans la notif (tous les membres)
- [] new season send notification to everyone when it begin


Match annulé : les familles ne sont jamais prévenues. MatchCancelledHandler récupère les familles qui avaient confirmé, mais le bloc qui devait en tirer les utilisateurs est vide (« on skip »). Seul le staff reçoit la notification. C’est probablement un bug, ou du moins un manque.
Replanification d’un match : aucune notification. Un TODO dans MatchesAdminController.Update dit « publish MatchRescheduled so Participation can notify registered families ».
Pas de notification dans les cas suivants : une réservation de covoiturage (le conducteur n’est pas prévenu), la révocation ou la modification d’un point manuel, et un passage du match à « joué ».
BroadcastRequest.TeamId est accepté mais ignoré. Seul DivisionId filtre les destinataires.
Le push est « best effort ». Une panne Firebase est loguée mais ne bloque pas l’action d’origine, et les tokens invalides sont supprimés.
