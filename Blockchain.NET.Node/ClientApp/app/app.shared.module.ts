import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpModule } from '@angular/http';
import { RouterModule } from '@angular/router';
import { InfiniteScrollModule } from 'angular2-infinite-scroll';

import { AppComponent } from './components/app/app.component';
import { NavMenuComponent } from './components/navmenu/navmenu.component';
import { WalletComponent } from './components/wallet/wallet.component';
import { BlockchainComponent } from './components/blockchain/blockchain.component';

@NgModule({
    declarations: [
        AppComponent,
        NavMenuComponent,
        BlockchainComponent,
        WalletComponent
    ],
    imports: [
        CommonModule,
        HttpModule,
        FormsModule,
        RouterModule.forRoot([
            { path: '', redirectTo: 'blockchain', pathMatch: 'full' },
            { path: 'wallet', component: WalletComponent },
            { path: 'blockchain', component: BlockchainComponent },
            { path: '**', redirectTo: 'home' }
        ]),
        InfiniteScrollModule
    ]
})
export class AppModuleShared {
}
