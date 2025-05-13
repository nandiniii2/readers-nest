import { Component, OnInit } from '@angular/core';
import { ApiService } from '../services/api.service';

export interface TableElement {
  name: string;
  value: string | undefined;
}

@Component({
  selector: 'profile',
  templateUrl: './profile.component.html',
  styleUrls: ['./profile.component.scss'],
})
export class ProfileComponent implements OnInit {
  dataSource: TableElement[] = [];
  columns: string[] = ['name', 'value'];

  constructor(private api: ApiService) {}

  ngOnInit() {
    let user = this.api.getTokenUserInfo();

    this.dataSource = [
      { name: 'Name', value: user?.firstName + ' ' + user?.lastName },
      { name: 'Email', value: user?.email ?? '' },
      { name: 'Mobile', value: user?.mobile },
      { name: 'Blocked', value: this.blockedStatus() },
      { name: 'Active', value: this.activeStatus() },
    ];
  }

  blockedStatus(): string {
    let bloked = this.api.getTokenUserInfo()!.blocked;
    return bloked ? 'YES, you are BLOCKED' : 'NO, you are not BLOCKED!';
  }

  activeStatus(): string {
    let active = this.api.getTokenUserInfo()!.active;
    return active
      ? 'YES, your account is ACTIVE'
      : 'NO, your account is not ACTIVE!';
  }
}
