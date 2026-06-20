import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { DocumentDto, SourceDto } from '../core/models';

interface ChatTurn {
  question: string;
  answer: string;
  sources: SourceDto[];
}

@Component({
  selector: 'app-workspace',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './workspace.component.html',
  styleUrls: ['./workspace.component.scss'],
})
export class WorkspaceComponent implements OnInit {
  private readonly api = inject(ApiService);
  readonly auth = inject(AuthService);

  documents = signal<DocumentDto[]>([]);
  conversation = signal<ChatTurn[]>([]);

  // Upload form.
  pasteName = 'notes.txt';
  pasteContent = '';
  uploading = signal(false);
  uploadError = signal<string | null>(null);

  // Chat form.
  question = '';
  asking = signal(false);

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.api.listDocuments().subscribe({
      next: (docs) => this.documents.set(docs),
      error: () => this.documents.set([]),
    });
  }

  uploadPasted(): void {
    if (!this.pasteContent.trim()) {
      return;
    }
    this.uploading.set(true);
    this.uploadError.set(null);
    this.api.uploadText(this.pasteName || 'notes.txt', this.pasteContent).subscribe({
      next: () => {
        this.uploading.set(false);
        this.pasteContent = '';
        this.refresh();
      },
      error: (err) => {
        this.uploading.set(false);
        this.uploadError.set(err?.error?.error ?? 'Upload failed.');
      },
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    this.uploading.set(true);
    this.uploadError.set(null);
    this.api.uploadFile(file).subscribe({
      next: () => {
        this.uploading.set(false);
        input.value = '';
        this.refresh();
      },
      error: (err) => {
        this.uploading.set(false);
        this.uploadError.set(err?.error?.error ?? 'Upload failed.');
      },
    });
  }

  ask(): void {
    const q = this.question.trim();
    if (!q) {
      return;
    }
    this.asking.set(true);
    this.api.query(q, 4).subscribe({
      next: (res) => {
        this.conversation.update((turns) => [
          { question: q, answer: res.answer, sources: res.sources },
          ...turns,
        ]);
        this.question = '';
        this.asking.set(false);
      },
      error: (err) => {
        this.conversation.update((turns) => [
          {
            question: q,
            answer: err?.error?.error ?? 'Query failed.',
            sources: [],
          },
          ...turns,
        ]);
        this.asking.set(false);
      },
    });
  }

  logout(): void {
    this.auth.clear();
  }
}
