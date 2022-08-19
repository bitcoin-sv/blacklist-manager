Copyright (c) 2020 Bitcoin Association

# Deploying Blacklist manager as docker container and setting it up for use

Steps described in **Running application** are for initial deployment of Blacklist manager

Steps described in **Configuring application** are for managing Blacklist manager

*Note: For cleaning up docker containers and removing Blacklist manager see [Removing docker images](doc/DockerImagesRemoving.md)*

BlacklistManager is a REST API that serves as an intermediary between bitcoind nodes and AlertManagers that are trusted by BlacklistManager.

## Running BM application

+ extract projectX package `projectX-BM.zip` from to target folder

+ run this commands in target folder to load docker images

  ``` bash
  docker load < blacklistmanagerapp.tar
  ```

  *On Windows use: docker load -i blacklistmanagerapp.tar*

+ create certificates folder. It should contain .crt files with with root and intermediate CA certificates that issued SSL server certificates used by Alert Manager.  Each certificate must be exported as a **Base-64 encoded X.509** file with a crt extension type. This step is required if Alert Manager uses SSL server certificates issued by untrusted CA (such as self signed certificate)

+ populate all environment variables in `.env` file in target folder

  | Parameter | Description |
  | ----------- | ----------- |
  | POSTGREDB_PASSWORD | Password of the postgres DB user that has create DB rights |
  | BMAPP_DB_PASSWORD | Password for database containing application data |
  | BLOCKCHAIN | Blockchain that BM is processing (BSV/BTC/BCH) |
  | BITCOIN_NETWORK | bitcoind network that app is configured for [RegTest\|Main\|TestNet] |
  | BMAPP_REST_APIKEY | api key that will be used by clients for REST authentication |
  | ENCRYPTION_KEY | Key used to encrypt private keys |
  | BACKGROUND_JOB_DELAY_TIME | Delay time between successful job runs (in seconds) |
  | CONSENSUS_ACTIVATION_DELAY_TIME | Delay time between consensus activations retrieval (in seconds) |
  | ERROR_RETRY_DELAY_TIME | Delay time if error occured while processing job (in seconds) |
  | CONSENSUS_WAIT_DAYS | Number of days that BM will keep trying to retrieve consensus activation from LEE |

+ run this command in target folder to start BlacklistManager application

  ``` bash
  docker-compose up -d
  ```

After running up the application for the first time, following steps have to be taken for BlacklistManager to use it's full potential as a tool that ensures that new court orders and funds it uses (by frezing/unfreezing) will be propagated to Bitcoin nodes. Steps that should be done in following order:

1. [Add Bitcoin node](#1.-add-bitcoind-node-connection-parameters)
2. [Add AlertManager's public key as trusted key](#2.-add-public-key-to-trust-list)
3. [Add signing key](#3.-adding-signing-key)
4. [Add miner key](#5.-adding-miner-key-to-signing-key)
5. [Validate and activate miner key](#6.-validate-and-activate-miner-key)
6. [Add connection to desired AlertManager](#7.-Add-alertmanager-endpoint-connection-parameters)
7. [Check everything is set up properly](#8.-test-everything-in-blacklist-manager-is-set-up)

## Configuring BM application

BlacklistManager REST API can be accessed by using BlacklistManager CLI. To get all available commands, user can get help menu with this command

```bash
docker exec bmapp /app/cli/BlacklistManager.Cli bmapp -h
```

### 1. Add bitcoind node connection parameters

Bitcoin nodes have to be added to BlacklistManager so that nodes handling the transactions can be made aware that specific coins cannot be spent if they were marked as frozen by court orders, hence rejecting such transactions. Same nodes will also be notified when coins can be spent again.

*Note: Before bitcoind node is added to database, BlacklistManager performs a connection check to the endpoint.*

Usage:

``` bash
docker exec bmapp /app/cli/BlacklistManager.Cli bmapp POST Node "{ \"id\" : \"[host:port]\", \"username\": \"[username]\", \"password\": \"[password]\", \"remarks\":\"[remarks]\" }" --apiKey=[apikey]
```

Parameter desription:

| Parameter | Description |
| ----------- | ----------- |
| [host:port] | bitcoind host and RPC port |
| [username]/[password] | bitcoind RPC username and password |
| [remarks]| additional remarks |
| [apiKey] | environment variable BMAPP_REST_APIKEY as defined in .env file |

### 2. Add public key to trust list

Each AlertManager has it's own private key, which is used to sign all data sensitive JSON messages that are returned when calling AlertManager's REST API. For BlacklistManager to validate that such messages returned by AlertManager are indeed valid it has to store public key pair for each AlertManager that it will be calling.

Usage:

``` bash
docker exec bmapp /app/cli/BlacklistManager.Cli bmapp POST Trustlist "{ \"id\" : \"[publickey]\"  }" --apiKey=[apikey]
```

Parameter desription:

| Parameter | Description |
| ----------- | ----------- |
| [publickey] | WIF representation of public key part of AlertManager environment variable NTAPP_PRIVATE_KEY |
| [apiKey] | environment variable BMAPP_REST_APIKEY as defined in .env file |

### 3. Adding signing key

BlacklistManager must send sensitive data to AlertManager (for example JSON document for Court order acceptance). To sign this data, active signing key must be present in database. User can add a new private key to BlacklistManager anytime, but upon activation of this new key, the old one will be deactivated.

Two types of signing keys can be added. Which type is added is controlled by field **delegationRequired** with values:

+ ***true*** - Miner keys have to be [added](#5.-adding-miner-key-to-signing-key) before the signer key can be [activated](#6.-validate-and-activate-miner-key)
+ ***false*** - Signing key is activated right away and no miner keys are required

*Note:*

+ *One private key with **delegationRequired=true** is generated by default when starting the BlacklistManager for the first time. It can be looked up by calling GET method signingKey.*

  It can be looked up by calling GET method signingKey (see Usage on next step [4. Get signing keys](#4.-get-signing-keys)).*

+ *If user decides to use the default private key provided, then this step "POST signingKey" can be skipped.*

+ Notice should be taken that when adding private keys through CLI, care should be taken not to leak the private keys. Keys should either be read from standard input, or user should disable shell history with ```set +o history``` command

Usage:

``` bash
docker exec bmapp /app/cli/BlacklistManager.Cli bmapp POST signingKey "{ \"privateKey\" : \"[privateKey]\", \"delegationRequired\" : true  }" --apiKey=[apiKey]
```

Parameter desription:

| Parameter | Description |
| ----------- | ----------- |
| [privateKey] | signing private key that BM uses for delegation key logic |
| [delegationRequired] | if set to false, the key is activated right away and no miner keys are required. If set to true, miner keys have to be imported and validated before they can be used |
| [apiKey] | environment variable BMAPP_REST_APIKEY as defined in .env file |

### 4. Get signing keys

Get all signing keys from database with their relevant data *(private key is not returned)*

'signerId' parameter that is returned in JSON response must be used as [signerId] parameter in subseguent calls to signingKey api

Usage:

```bash
docker exec bmapp /app/cli/BlacklistManager.Cli bmapp GET signingKey --apiKey=[apiKey]
```

Parameter desription:

| Parameter | Description |
| ----------- | ----------- |
| [apiKey] | environment variable BMAPP_REST_APIKEY as defined in .env file |

### 5. Adding miner key to signing key

Miner key is added by providing either **publicKey** or **publicKeyAddress** (one of them must be set...also both can be set, but address must be derived from that same public key ). Miner key is used by AlertManager when trying to determine how many blocks have been "mined" with this key. This information is used by the user of AlertManager, when it tries to determine how much hash power has voted in favor of a court order.

As a response the method returns JSON document, where the content of **payload** field must be signed as specified by [JSON Envelope specification](https://github.com/bitcoin-sv-specs/brfc-misc/tree/master/jsonenvelope) or with bitcoind RPC method **signmessage** as described [here](#9.-sign-payload-with-bitcoind-signmessage-method).

"mined" block is refered to one of the following things:

+ coinbase transaction used public key in P2PKH script
+ coinbase transaction sent reward to public key address
+ coinbase transaction contains [MinerId coinbase document](https://github.com/bitcoin-sv-specs/brfc-minerid) where minerId field cointains publicKey

*Note: In test environemnt Public key address can be obtained by calling bitcoind's RPC method **getnewaddress**.*

Usage:

```bash
docker exec bmapp /app/cli/BlacklistManager.Cli bmapp POST signingKey/[signerId]/minerKey "{ \"publicKey\" : \"[publicKey]\" \"publicKeyAddress\" : \"[publicKeyAddress\"  }" --apiKey=[apiKey]
```

Parameter desription:

| Parameter | Description |
| ----------- | ----------- |
| [signerId] | signer id returned in step [Adding signing key](#3.-adding-signing-key)|
| [publicKey] | miner public key that will be used for delegation logic (either 'publicKey' or 'publicKeyAddress' must be set) |
| [publicKeyAddress] | miner public key address BM uses for delegation key logic (either 'publicKey' or 'publicKeyAddress' must be set) |
| [apiKey] | environment variable BMAPP_REST_APIKEY as defined in .env file |

### 6. Validate and activate miner key

Each miner key that was inserted into BlacklistManager must be validated before it can be used. Validation is performed by verifying if **signature** that was provided, matches with **publicKey/publicKeyAddress** (that was sent in a request when [adding new miner key](#5.-adding-miner-key-to-signing-key)) and **payload** (that was sent as a response when [adding new miner key](#5.-adding-miner-key-to-signing-key)).

Multiple miner keys can be added to a signer key. When the miner key/signer key combination is ready to be used by BlacklistManager the **activateKey** field in the request should be set to true.

*Note: When activating a new miner key/signer key combination, previous key combination will be deactivated and all new documents sent from BlacklistManager will use only this new key combination*

Usage:

```bash
docker exec bmapp /app/cli/BlacklistManager.Cli bmapp PUT signingKey/[signerId]/minerKey "{ \"id\":[minerId], \"signature\" : \"[signature]\", \"activateKey\" : true }" --apiKey=[apikey]
```

Parameter desription:

| Parameter | Description |
| ----------- | ----------- |
| [signerId] | signer id returned in step [Adding signing key](#3.-adding-signing-key)|
| [minerId] | minerId that was returned in step [Adding miner key to signing key](#5.-adding-miner-key-to-signing-key) |
| [signature] | signature of payload from step [Adding miner key to signing key](#5.-adding-miner-key-to-signing-key) |
| [activateKey] | is signerKey/minerKey combination activated for use [true\|false] |
| [apiKey] | environment variable BMAPP_REST_APIKEY as defined in .env file |

### 7. Add AlertManager endpoint connection parameters

BlacklistManager must have at least one AlertManager endpoint before it can start to download any court orders that have to be enforced by bitcoin nodes.

*Note: Before AlertManager endpoint is added to database, BlacklistManager performs a connection check to the endpoint, and if successfull it also verifies if the returned public key is trusted and signature is valid.*

Usage:

```bash
docker exec bmapp /app/cli/BlacklistManager.Cli bmapp POST  LegalEntityEndpoint "{ \"baseUrl\" : \"[https://host:port]\", \"apiKey\" : \"[leeApiKey]\"  }" --apiKey=[apikey]
```

Parameter desription:

| Parameter | Description |
| ----------- | ----------- |
| [https://host:port] | AlertManager endpoint URL  |
| [leeApiKey] | client authentication key as defined in AlertManager client settings |
| [apiKey] | environment variable BMAPP_REST_APIKEY as defined in .env file |

### 8. Test everything in blacklist manager is set up

To test if BlacklistManager is properly set up to be used **status** method can be called. JSON document that was returned must not contain any **Error** messages. If error messages were returned, they must be addressed before BM will be able to perform it's job.

JSON can also contain **Warning** messages or **Info** messages, but they are not critical.

Sample of returned error:

```json
  {
    "component": "Alert Manager",
    "endpoint": "https://endpoint",
    "severity": "Warning",
    "message": "No blocks have been mined with 'xxxxxxxxxxxxxxxxxxxxxxxx' public key address."
  }
```

Usage:

```bash
docker exec bmapp /app/cli/BlacklistManager.Cli bmapp GET  status --apiKey=[apikey]
```

### 9. Sign payload with bitcoind signmessage method

BlacklistManager allows miner key verification by using bitcoind's **verifymessage** RPC method. User has 3 ways, how to get the valid signature:

+ By using bitcoind's **signmessage** RPC method (address from bitcoind's wallet must be used):

  ``` bash
  bitcoin-cli -conf=bitcoin.conf signmessage migm7fzWbve1V4qia8SVH16xeE4599zDRx  "{\"documentType\":\"delegatedKeys\",\"createdAt\":\"2020-07-01T07:46:29.2769522Z\",\"delegatingPublicKey\":null,\"delegatingPublicKeyAddress\":\"migm7fzWbve1V4qia8SVH16xeE4599zDRx\",\"purpose\":\"legalEntityCommunication\",\"delegatedPublicKeys\":[\"025720793ce9cece519837ac4743e953c4b886919057eb94ab6baa98451cc1d0db\"]}"
  ```

+ By using bitcoind's **signmessagewithprivkey** RPC method (privatekey from [public key](#5.-adding-miner-key-to-signing-key) that is being added must be used)

  ``` bash
  bitcoin-cli -conf=bitcoin.conf signmessagewithprivkey cP2L3eieVm2BdyurcrSR5FtLr6ft4oNcxoAEzhxjMEd7c9VJCMVM  "{\"documentType\":\"delegatedKeys\",\"createdAt\":\"2020-07-01T07:46:29.2769522Z\",\"delegatingPublicKey\":null,\"delegatingPublicKeyAddress\":\"migm7fzWbve1V4qia8SVH16xeE4599zDRx\",\"purpose\":\"legalEntityCommunication\",\"delegatedPublicKeys\":[\"025720793ce9cece519837ac4743e953c4b886919057eb94ab6baa98451cc1d0db\"]}"
  ```

+ By using ElectrumX's wallet GUI and signing the payload there

Since Bitcoin CORE creates segwit addresses by default (unless using the `legacy` option) and the **signmessage** RPC method is still unable to produce signatures as described [here](#https://github.com/bitcoin/bitcoin/issues/10542), private key and **signmessagewithprivkey** RPC method has to be used in this cases. To extract private key from your wallet with segwit address **dumpprivkey** RPC method has to be used:

```bash
bitcoin-cli -conf=bitcoin.conf dumpprivkey bcrt1qayhksg6gkmvqhggntu5hm7wkd7c42supm0tkhp
```
