import { Component, Inject } from '@angular/core';
import { Http } from '@angular/http';
import { Observable } from 'rxjs';
import 'rxjs/add/observable/interval';
import 'rxjs/add/operator/map';

@Component({
    selector: 'blockchain',
    templateUrl: './blockchain.component.html',
    styleUrls: ['./blockchain.component.css']
})
export class BlockchainComponent {
    range: number[] = [];
    constructor(private http: Http, @Inject('BASE_URL') private baseUrl: string) {
        for (var i = 0; i < 50; i++) {
            this.range.push(i);
        }
        this.onScrollBlocks();
        this.onScrollTransactions();
        this.loadMinerState();
        Observable.interval(1000)
            .takeWhile(() => true)
            .subscribe(i => {
                this.loadLiveMinerInfo();
            });
        this.loadGeneralInfo()
        this.sendNodeUrl();
    }

    public isLoadingBlocks: boolean = false;
    public isLoadingTransactions: boolean = false;
    public loadedBlocks: Block[] = [];
    public loadedTransactions: Transaction[] = [];
    public lastLoadedBlock = 2147483647;
    public lastLoadedTransaction = 2147483647;
    //public loadedTransactions: Transaction[] = [];
    public minerState: boolean = false;
    public actualInformation: string[];
    public generalInfo: string[];

    onScrollBlocks() {
        if (!this.isLoadingBlocks && this.lastLoadedBlock > 1) {
            this.isLoadingBlocks = true;
            this.http.get(this.baseUrl + 'api/v1/dashboard/getblocks/' + this.lastLoadedBlock)
                .map(response => { return response.json(); })
                .subscribe(result => {
                    result.forEach((block: any) => {
                        this.loadedBlocks.push({
                            height: block.Height,
                            difficulty: block.Difficulty,
                            timeStamp: new Date(block.TimeStamp)
                        } as Block);
                        this.lastLoadedBlock = block.Height;
                    });
                    this.isLoadingBlocks = false;
                    this.lastLoadedBlock--;
                }, error => {
                    this.isLoadingBlocks = false;
                    console.error(error);
                });
        }
    }

    loadMinerState() {
        this.http.get(this.baseUrl + 'api/v1/dashboard/minerstate')
            .map(data => data.text() === 'true')
            .subscribe(result => {
                this.minerState = result;
            }, error => {
                console.error(error);
            });
    }

    loadLiveMinerInfo() {
        this.http.get(this.baseUrl + 'api/v1/dashboard/actualinformation')
            .map(data => data.text().replace('"', '').replace('"','').split(','))
            .subscribe(result => {
                this.actualInformation = result;

            }, error => {
                console.error(error);
            });
    }

    loadGeneralInfo() {
        this.http.get(this.baseUrl + 'api/v1/dashboard/generalinformation')
            .map(data => data.text().replace('"', '').replace('"', '').split(','))
            .subscribe(result => {
                this.generalInfo = result;

            }, error => {
                console.error(error);
            });
    }

    toggleMiner() {
        this.http.get(this.baseUrl + 'api/v1/dashboard/toggleminer')
            .map(data => data.text() === 'true')
            .subscribe(result => {
                this.minerState = result;
            }, error => {
                console.error(error);
            });
    }

    sendNodeUrl() {
        this.http.post(this.baseUrl + 'api/v1/dashboard/setnodeurl', { nodeUrl: this.baseUrl }).subscribe(result => {
        }, error => { console.log(error); });
    }

    onScrollTransactions() {
        if (!this.isLoadingTransactions && this.lastLoadedTransaction > 1) {
            this.isLoadingTransactions = true;
            this.http.get(this.baseUrl + 'api/v1/dashboard/gettransactions/' + this.lastLoadedTransaction)
                .map(response => { return response.json(); })
                .subscribe(result => {
                    result.forEach((transaction: any) => {
                        this.loadedTransactions.push({
                            receiver: transaction.Receiver,
                            id: transaction.Id,
                            amount: transaction.Amount,
                            isCoinBase: transaction.IsCoinbase
                        } as Transaction);
                        this.lastLoadedTransaction = transaction.Id;
                    });
                    this.isLoadingTransactions = false;
                    this.lastLoadedTransaction--;
                }, error => {
                    this.isLoadingTransactions = false;
                    console.error(error);
                });
        }
    }
}

class Block {
    height: number;
    difficulty: number;
    timeStamp: Date;
}

class Transaction {
    receiver: string;
    id: number;
    amount: number;
    isCoinBase: boolean;
}