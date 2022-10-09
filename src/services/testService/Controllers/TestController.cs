using Microsoft.AspNetCore.Mvc;
using Nethereum.Hex.HexTypes;
using Nethereum.KeyStore.Model;
using Nethereum.Model;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using System.IO;
using System.Numerics;
using testService.SmartContractsInteraction;

namespace Blockchain_DotNet_BackEnd_POC.Controllers;

[ApiController]
[Route("[controller]")]
public class TestController : ControllerBase
{
    private readonly ILogger<TestController> _logger;
    private const string _web3Url = "HTTP://127.0.0.1:8545";
    private Web3 _web3;

    public TestController(ILogger<TestController> logger)
    {
        _logger = logger;
        _web3 = new Web3(_web3Url);
    }

    [HttpGet("/balance/{address}")]
    //[Route("/balance/{address}")]
    public async Task<ActionResult<decimal>> Get(string address)
    {
        var balance = await _web3.Eth.GetBalance.SendRequestAsync(address);
        var etherAmount = Web3.Convert.FromWei(balance.Value);
        return Ok(etherAmount);
    }

    [HttpGet("/balance/{address}/transfer/{to}")]
    //[Route("/balance/{address}/transfer/{to}")]
    public async Task<ActionResult<decimal>> Transfer(string address, string to, decimal amount)
    {
        var privateKey = "2932c818a41d2c659719445345039d8c67eff41413d88b0c9759b9483f638676";
        var account = new Nethereum.Web3.Accounts.Account(privateKey, Nethereum.Signer.Chain.Private);
        _web3 = new Web3(account);
        _web3.TransactionManager.UseLegacyAsDefault = true;
        var balance = await _web3.Eth.GetBalance.SendRequestAsync(address);
        var etherAmount = Web3.Convert.FromWei(balance.Value);

        var amountBigInteger = Web3.Convert.ToWei(amount);

        if(etherAmount < amount)
            return BadRequest();

        var result = await _web3.TransactionManager.SendTransactionAsync(account.Address, to, new HexBigInteger(amountBigInteger));

        // await _web3.Eth.GetEtherTransferService()
        //     .TransferEtherAndWaitForReceiptAsync(to, amount);
        
        return Ok(result);
    }

    [HttpPost("/KeyStore")]
    //[Route("/balance/{address}/transfer/{to}")]
    public async Task CreateKeyStore()
    {
        if(System.IO.File.Exists("StoreKey.json"))
            return;

        var keyStoreService = new Nethereum.KeyStore.KeyStoreScryptService();
        var scryptParams = new ScryptParams {Dklen = 32, N = 262144, R = 1, P = 8};
        var ecKey = Nethereum.Signer.EthECKey.GenerateKey();
        var password = "testPassword";

        var keyStore = keyStoreService.EncryptAndGenerateKeyStore(password, ecKey.GetPrivateKeyAsBytes(), ecKey.GetPublicAddress(), scryptParams);
        var json = keyStoreService.SerializeKeyStoreToJson(keyStore);

        await System.IO.File.WriteAllTextAsync("StoreKey.json", json);
    }

    [HttpGet("/KeyStore")]
    //[Route("/balance/{address}/transfer/{to}")]
    public async Task<string> DecryptKeyStore()
    {
        if(!System.IO.File.Exists("StoreKey.json"))
            return string.Empty;

        var json = await System.IO.File.ReadAllTextAsync("StoreKey.json");

        var keyStoreService = new Nethereum.KeyStore.KeyStoreScryptService();
        var password = "testPassword";
        var key = keyStoreService.DecryptKeyStoreFromJson(password, json);

       return System.Text.Encoding.Default.GetString(key);
    }

    [HttpPost("/deploy")]
    //[Route("/balance/{address}/transfer/{to}")]
    public async Task<ActionResult<string>> Deploy()
    {
        if(!System.IO.File.Exists("StoreKey.json"))
            return BadRequest();

        var json = await System.IO.File.ReadAllTextAsync("StoreKey.json");

        var keyStoreService = new Nethereum.KeyStore.KeyStoreScryptService();
        var password = "testPassword";
        var key = keyStoreService.DecryptKeyStoreFromJson(password, json);

        var account = new Nethereum.Web3.Accounts.Account(key, Nethereum.Signer.Chain.Private);
        _web3 = new Web3(account, _web3Url);
        _web3.TransactionManager.UseLegacyAsDefault = true;

        var deploymentMessage = new StandardTokenDeployment
        {
            TotalSupply = 100
        };

        var deploymentHandler = _web3.Eth.GetContractDeploymentHandler<StandardTokenDeployment>();
        var transactionReceipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(deploymentMessage);
        var contractAddress = transactionReceipt.ContractAddress;
        return Ok(contractAddress);
    }

	[HttpGet("/standardToken/{contractAddress}/Balance/{address}")]
	//[Route("/balance/{address}/transfer/{to}")]
	public async Task<ActionResult<BigInteger>> GetBalanceStandardToken(string address, string contractAddress)
	{
		var balanceOfFunctionMessage = new BalanceOfFunction()
        {
			Owner = address,
		};

		var balanceHandler = _web3.Eth.GetContractQueryHandler<BalanceOfFunction>();
		var balance = await balanceHandler.QueryAsync<BigInteger>(contractAddress, balanceOfFunctionMessage);

        return Ok(balance.ToString());
	}

	[HttpPost("/standardToken/{contractAddress}/Balance/transfer/{to}")]
	//[Route("/balance/{address}/transfer/{to}")]
	public async Task<ActionResult<bool>> TransferStandardToken(string to, string contractAddress)
	{
		if (!System.IO.File.Exists("StoreKey.json"))
			return BadRequest();

		var json = await System.IO.File.ReadAllTextAsync("StoreKey.json");

		var keyStoreService = new Nethereum.KeyStore.KeyStoreScryptService();
		var password = "testPassword";
		var key = keyStoreService.DecryptKeyStoreFromJson(password, json);

		var account = new Nethereum.Web3.Accounts.Account(key, Nethereum.Signer.Chain.Private);
		_web3 = new Web3(account, _web3Url);
		_web3.TransactionManager.UseLegacyAsDefault = true;

		var transferHandler = _web3.Eth.GetContractTransactionHandler<TransferFunction>();
		var transfer = new TransferFunction()
		{
			To = to,
			TokenAmount = 1
		};
		var transactionReceipt = await transferHandler.SendRequestAndWaitForReceiptAsync(contractAddress, transfer);

        return Ok(!transactionReceipt.HasErrors());
	}

	[HttpGet("/standardToken/{contractAddress}/Balance/signedtransfer/{to}")]
	//[Route("/balance/{address}/transfer/{to}")]
	public async Task<ActionResult<string>> GetSignedTransactionTransferStandardToken(string to, string contractAddress)
	{
		if (!System.IO.File.Exists("StoreKey.json"))
			return BadRequest();

		var json = await System.IO.File.ReadAllTextAsync("StoreKey.json");

		var keyStoreService = new Nethereum.KeyStore.KeyStoreScryptService();
		var password = "testPassword";
		var key = keyStoreService.DecryptKeyStoreFromJson(password, json);

		var account = new Nethereum.Web3.Accounts.Account(key, Nethereum.Signer.Chain.Private);
		_web3 = new Web3(account, _web3Url);
		_web3.TransactionManager.UseLegacyAsDefault = true;

		var transferHandler = _web3.Eth.GetContractTransactionHandler<TransferFunction>();
		var transfer = new TransferFunction()
		{
			To = to,
			TokenAmount = 1
		};

		//If we don't provide the nonce, gas, etc Nethereum needs to connect to a node retrieve the information, 
		//so signing is not fully offline
		var signedTransaction1 = await transferHandler.SignTransactionAsync(contractAddress, transfer);
		//await _web3.Eth.Transactions.SendRawTransaction.SendRequestAsync("0x" + signedTransaction1);

		return Ok("0x" + signedTransaction1);
	}

	[HttpPost("/signedtransaction/{signedTransaction}")]
	//[Route("/balance/{address}/transfer/{to}")]
	public async Task<ActionResult<string>> ExecuteSignedTransaction(string signedTransaction)
	{
		var transactionIdPredicted = Nethereum.Util.TransactionUtils.CalculateTransactionHash(signedTransaction);
		var transactionId = await _web3.Eth.Transactions.SendRawTransaction.SendRequestAsync(signedTransaction);

		Console.WriteLine($"Predicted transaction hash: {transactionIdPredicted}");
		Console.WriteLine($"Actual transaction hash: {transactionId}");

		return Ok(transactionId);
	}
}
