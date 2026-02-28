# Ephec-TCP
1. Architecture et Organisation du Code :
   - Séparation des responsabilités : Création d'une bibliothèque Shared pour centraliser les modèles et le protocole, évitant la duplication de code entre le Transmitter et le Receiver.
   - Classe FileSegment dédiée : Implémentation de la gestion disque dans Shared/Protocol/Files, isolant les écritures FileStream du moteur réseau.
   - Gestion multi-connexions : Utilisation d'un ConcurrentDictionary dans TcpConnection pour stocker les ConnectionInfo par adresse IP, permettant au serveur de gérer plusieurs transferts simultanés.

2. Handshake TCP (Ouverture de Connexion) :
    -  Algorithme en 3 étapes (Three-way Handshake) : Implémentation complète du cycle SYN → SYN-ACK → ACK.
    -  Calcul des séquences (x et y) :Génération d'un ISN aléatoire ($x$ pour le client, $y$ pour le serveur).Incrémentation stricte x+1 et y+1 pour les accusés de réception.
    -  Validation de l'état : La connexion n'est déclarée "ÉTABLIE" côté serveur qu'après la réception de l'ACK final du client (étape 5 du schéma).
  

 3. Transfert de Données Fiable :
    - Mécanisme "Stop-and-Wait" : Envoi d'un segment de 1024 octets suivi d'une attente bloquante de l'ACK avant d'envoyer le suivant.
    - Calcul d'offset précis : Utilisation du numéro de séquence pour calculer l'emplacement exact dans le fichier : Offset = (SequenceNumber - (InitialSequence + 1)) \times 1024
    - Synchronisation via TaskCompletionSource : Utilisation d'un signal asynchrone pour débloquer la boucle d'envoi dès que l'ACK est reçu par le UdpTransport.
  
   
4. Robustesse et Gestion des États :
    - Machine à états finis : Utilisation d'un enum State (SynSent, SynReceived, Established, WaitingAck, etc.) pour contrôler le comportement du protocole.
    - Gestion des Timeouts : Implémentation d'un délai de garde de 5000 ms. Si aucun ACK ne revient, le transfert s'arrête proprement au lieu de figer.
    - Sécurité anti-crash : Vérification systématique de l'existence du fichier source avant le démarrage et gestion des exceptions lors de l'écriture disque.
   
    
5. Visibilité et Diagnostic (Terminal) :
    - Logs bidirectionnels : Affichage clair des segments envoyés ([TX]) et reçus ([RX]) avec leurs numéros de séquence et flags.
    - Messages d'état : Notification en temps réel des transitions importantes ([ÉTAT] Connexion établie, Transfert terminé).
    - Indicateurs de progression : Affichage de la taille de chaque segment (ex: 1024 octets) pour confirmer que le découpage du fichier fonctionne.
    
6. Fermeture de Connexion : 
   - Flag FIN : Envoi d'un segment de fin de session après la lecture complète du fichier pour notifier le récepteur de fermer le flux.
