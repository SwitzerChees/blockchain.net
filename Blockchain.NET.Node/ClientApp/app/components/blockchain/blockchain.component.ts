import { Component, Inject } from '@angular/core';
import { Http } from '@angular/http';
import { Observable } from 'rxjs';
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
    }

    public isLoadingBlocks: boolean = false;
    public isLoadingTransactions: boolean = false;
    public loadedBlocks: Block[] = [];
    public lastLoadedBlock = 2147483647;

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

    onScrollTransactions() {
        console.log('scrolled transactions!!');
        //this.http.get(this.baseUrl + 'api/wallet/WalletBalance').subscribe(result => {
        //    this.walletBalance = result.json() as WalletBalance;
        //}, error => console.error(error));
    }
}

class Block {
    height: number;
    difficulty: number;
    timeStamp: Date;
}